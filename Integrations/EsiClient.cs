using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using data_registry_public.Infrastructure.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace data_registry_public.Integrations
{
    /// <summary>
    /// Настройки ЕСИ (секция Esi в appsettings.{режим}.json).
    /// Test и Production — боевой контур esia.tunduk.kg, Development — DEV-контур dev-esia.tunduk.kg; клиент Record_Owners.
    /// для боевого контура меняются адреса, ClientId/ClientSecret и RedirectUrl.
    /// </summary>
    public sealed class EsiOptions
    {
        public string AuthorizeUrl { get; set; } = "";
        public string TokenUrl { get; set; } = "";
        public string UserInfoUrl { get; set; } = "";
        /// <summary>/connect/endsession — завершение сессии в ЕСИ при выходе с портала.</summary>
        public string EndSessionUrl { get; set; } = "";
        /// <summary>
        /// Куда ЕСИ вернёт после выхода (post_logout_redirect_uri) — должен быть зарегистрирован в ЕСИ для клиента.
        /// Пусто — ЕСИ покажет свою страницу «Вы вышли».
        /// </summary>
        public string PostLogoutRedirectUrl { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        /// <summary>Адрес возврата — должен совпадать с зарегистрированным в ЕСИ символ в символ.</summary>
        public string RedirectUrl { get; set; } = "";
        public string Scope { get; set; } = "openid profile email phone";
        /// <summary>
        /// prompt (OpenID Connect). Пусто — стандартно: открытая сессия ЕСИ подхватывается без формы входа,
        /// а выход с портала (и отказ во входе) закрывает её через EndSessionUrl.
        /// login на esia.tunduk.kg при открытой сессии уводит на страницу профиля ЕСИ — не использовать.
        /// </summary>
        public string Prompt { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 15;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AuthorizeUrl) && !string.IsNullOrWhiteSpace(TokenUrl)
            && !string.IsNullOrWhiteSpace(UserInfoUrl) && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RedirectUrl);
    }

    /// <summary>Токены из /connect/token: access_token — для userinfo, id_token — подсказка ЕСИ при выходе.</summary>
    public sealed record EsiTokens(string AccessToken, string? IdToken);

    /// <summary>Параметры, которые надо сохранить между уходом в ЕСИ и возвратом (state + PKCE verifier).</summary>
    public sealed record EsiAuthorizeRequest(string Url, string State, string CodeVerifier);

    /// <summary>Данные пользователя из /connect/userinfo (поля по стандарту доступа к ЕСИ).</summary>
    public sealed class EsiUserInfo
    {
        public string? Sub { get; init; }
        public string? Pin { get; init; }
        public string? Name { get; init; }
        public string? FamilyName { get; init; }
        public string? GivenName { get; init; }
        public string? MiddleName { get; init; }
        public string? Gender { get; init; }
        public string? Birthdate { get; init; }
        public string? Citizenship { get; init; }
        public string? Email { get; init; }
        public string? EmailVerified { get; init; }
        public string? PhoneNumber { get; init; }
        public string? PhoneNumberVerified { get; init; }
        public string? OrganizationTin { get; init; }
        public string? OrganizationName { get; init; }
    }

    /// <summary>
    /// Вход через ЕСИ: Authorization Code flow + PKCE (S256), client_secret — Basic Authorization.
    /// Секреты, коды и токены в логи не пишутся; ПИН — только маской.
    /// </summary>
    public sealed class EsiClient
    {
        public const string HttpClientName = "esi";

        private readonly IHttpClientFactory _httpFactory;
        private readonly EsiOptions _options;
        private readonly ILogger<EsiClient> _logger;

        public EsiClient(IHttpClientFactory httpFactory, IOptions<EsiOptions> options, ILogger<EsiClient> logger)
        {
            _httpFactory = httpFactory;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _options.IsConfigured;

        /// <summary>
        /// Ссылка на /connect/endsession: ЕСИ закрывает свою сессию и возвращает на PostLogoutRedirectUrl.
        /// null — выход в ЕСИ не настроен.
        /// </summary>
        public string? CreateEndSessionUrl(string? idToken)
        {
            if (string.IsNullOrWhiteSpace(_options.EndSessionUrl)) return null;
            var query = new Dictionary<string, string?>();
            // id_token_hint: без него ЕСИ спрашивает «Выйти?» и не возвращает на портал.
            if (!string.IsNullOrWhiteSpace(idToken)) query["id_token_hint"] = idToken;
            if (!string.IsNullOrWhiteSpace(_options.PostLogoutRedirectUrl))
            {
                query["post_logout_redirect_uri"] = _options.PostLogoutRedirectUrl;
            }
            return QueryHelpers.AddQueryString(_options.EndSessionUrl, query);
        }

        /// <summary>Ссылка на /connect/authorize с новыми state и code_challenge.</summary>
        public EsiAuthorizeRequest CreateAuthorizeRequest()
        {
            var state = Base64Url(RandomNumberGenerator.GetBytes(24));
            // code_verifier: 43–128 символов из [A-Za-z0-9-._~]; 32 случайных байта → 43 символа base64url.
            var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

            var query = new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUrl,
                ["scope"] = _options.Scope,
                ["state"] = state,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256"
            };
            if (!string.IsNullOrWhiteSpace(_options.Prompt))
            {
                query["prompt"] = _options.Prompt;
            }
            var url = QueryHelpers.AddQueryString(_options.AuthorizeUrl, query);
            return new EsiAuthorizeRequest(url, state, verifier);
        }

        /// <summary>Обмен кода авторизации на токены. null — не удалось (причина в логе integration).</summary>
        public async Task<EsiTokens?> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
        {
            using var _ = LogCategories.Push(LogCategories.Integration);
            var sw = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "authorization_code",
                        ["code"] = code,
                        ["redirect_uri"] = _options.RedirectUrl,
                        ["code_verifier"] = codeVerifier
                    })
                };
                request.Headers.Authorization = BasicAuth();

                using var response = await Client().SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                using var json = TryParse(body);

                var token = json?.RootElement.TryGetProperty("access_token", out var at) == true ? at.GetString() : null;
                if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation("ЕСИ: токен получен (HTTP {Status}) за {Elapsed} мс", (int)response.StatusCode, sw.ElapsedMilliseconds);
                    return new EsiTokens(token, Field(json, "id_token"));
                }

                // Тело ответа целиком не пишем — только код и поля error/error_description.
                _logger.LogWarning("ЕСИ: токен не выдан — HTTP {Status} за {Elapsed} мс, error: {Error} {Description}",
                    (int)response.StatusCode, sw.ElapsedMilliseconds, Field(json, "error"), Field(json, "error_description"));
                return null;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("ЕСИ: token endpoint не ответил за {Timeout} с", _options.TimeoutSeconds);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ЕСИ: ошибка запроса токена ({Elapsed} мс)", sw.ElapsedMilliseconds);
                return null;
            }
        }

        /// <summary>
        /// Данные пользователя. Стандартный OpenID Connect — GET (так работает в Register_2 на проде);
        /// стандарт доступа к ЕСИ описывает POST — на ответ 405 повторяем запрос POST-ом.
        /// </summary>
        public async Task<EsiUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
        {
            using var _ = LogCategories.Push(LogCategories.Integration);
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await SendUserInfoAsync(HttpMethod.Get, accessToken, ct);
                if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    response.Dispose();
                    response = await SendUserInfoAsync(HttpMethod.Post, accessToken, ct);
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("ЕСИ: userinfo вернул HTTP {Status} за {Elapsed} мс", (int)response.StatusCode, sw.ElapsedMilliseconds);
                        return null;
                    }

                    var body = await response.Content.ReadAsStringAsync(ct);
                    using var json = TryParse(body);
                    if (json is null)
                    {
                        _logger.LogWarning("ЕСИ: userinfo вернул не JSON ({Length} симв.)", body.Length);
                        return null;
                    }

                    var info = new EsiUserInfo
                    {
                        Sub = Field(json, "sub"),
                        Pin = Field(json, "pin"),
                        Name = Field(json, "name"),
                        FamilyName = Field(json, "family_name"),
                        GivenName = Field(json, "given_name"),
                        MiddleName = Field(json, "middle_name"),
                        Gender = Field(json, "gender"),
                        Birthdate = Field(json, "birthdate"),
                        Citizenship = Field(json, "citizenship"),
                        Email = Field(json, "email"),
                        EmailVerified = Field(json, "email_verified"),
                        PhoneNumber = Field(json, "phone_number"),
                        PhoneNumberVerified = Field(json, "phone_number_verified"),
                        OrganizationTin = Field(json, "organization_tin"),
                        OrganizationName = Field(json, "organization_name")
                    };
                    _logger.LogInformation("ЕСИ: данные пользователя получены за {Elapsed} мс (ПИН {Pin}, ИНН организации {Tin})",
                        sw.ElapsedMilliseconds, AppLogging.Mask(info.Pin), info.OrganizationTin ?? "-");
                    return info;
                }
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("ЕСИ: userinfo не ответил за {Timeout} с", _options.TimeoutSeconds);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ЕСИ: ошибка запроса userinfo ({Elapsed} мс)", sw.ElapsedMilliseconds);
                return null;
            }
        }

        private Task<HttpResponseMessage> SendUserInfoAsync(HttpMethod method, string accessToken, CancellationToken ct)
        {
            var request = new HttpRequestMessage(method, _options.UserInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return Client().SendAsync(request, ct);
        }

        private HttpClient Client()
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
            return http;
        }

        /// <summary>client_id:client_secret в Basic (RFC 6749 §2.3.1: значения предварительно url-кодируются).</summary>
        private AuthenticationHeaderValue BasicAuth()
        {
            var raw = $"{Uri.EscapeDataString(_options.ClientId)}:{Uri.EscapeDataString(_options.ClientSecret)}";
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        }

        private static JsonDocument? TryParse(string body)
        {
            try { return string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body); }
            catch (JsonException) { return null; }
        }

        /// <summary>Строковое значение поля (числа/булевы — как текст), null если поля нет.</summary>
        private static string? Field(JsonDocument? json, string name)
        {
            if (json is null || json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty(name, out var v))
            {
                return null;
            }
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => v.GetRawText()
            };
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
