using data_registry_local.Models;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace data_registry_public.Controllers
{
    [Route("[controller]/[action]")]
    public class esiController : Controller
    {

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _appSettings;
        private readonly ILogger<esiController> _logger;
        private readonly AppDbContext _db;

        public esiController(IHttpClientFactory httpClientFactory, IOptions<AppSettings> appSettings, ILogger<esiController> logger, AppDbContext db)
        {
            _httpClientFactory = httpClientFactory;
            _appSettings = appSettings.Value;
            _logger = logger;
            _db = db;
        }




        [HttpGet]
        public IActionResult Login()
        {

            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "UserPage");
            }

            // Generate state, code verifier, and code challenge
            string state = Guid.NewGuid().ToString();
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Store code verifier and state in TempData
            TempData["code_verifier"] = codeVerifier;
            TempData["state"] = state;

            // Construct the authorization URL
            string authorizeUrl = $"{_appSettings.authorizeUrl}?response_type=code&client_id={_appSettings.clientId}&redirect_uri={_appSettings.redirectUrl}&scope=openid profile email phone&code_challenge={codeChallenge}&code_challenge_method=S256";

            return Redirect(authorizeUrl);
        }



        /// <summary>
        /// тестовый вход
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> LoginTest()
        {
            PublicUser userModel = new PublicUser();
            userModel.Email = "djabaeva.a@gmail.com";
            userModel.EsiEmail = "djabaeva.a@gmail.com";
            userModel.Name = "Айдана";
            userModel.EsiPin = "01001202210023";
            userModel.EsiName = "Айдана";
            userModel.EsiBirthdate = "01.01.2000";
            userModel.OrganizationTin = "01001202210023";
            //регистрирую пользователя
            //string newUserId = Guid.NewGuid().ToString();
            string userId = await registrateNewUser(userModel);
            if (userId != null && userId != "")
            {
                //аутентификация
                userModel.Id = userId;
                await Authenticate(userModel);
                _logger.LogInformation("Пользователь " + userModel.Name + " успешно авторизовался через ЕСИ");
                //await _journalling.AddToJournalAsynk(userModel?.Name, "Авторизация через ЕСИ");
                return RedirectToAction("Index", "UserPage");
            }

            return RedirectToAction("Index", "Landing");
        }


        private string GenerateCodeVerifier()
        {
            byte[] randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Base64UrlEncode(randomBytes);
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Base64UrlEncode(challengeBytes);
            }
        }

        private string Base64UrlEncode(byte[] bytes)
        {
            var base64 = Convert.ToBase64String(bytes);
            return base64.Replace("+", "-").Replace("/", "_").Replace("=", "");
        }


        [HttpGet]
        //[Route("signin-esi")]
        public async Task<IActionResult> auth(string code, string state)
        {
            try
            {
                // Verify state and retrieve code verifier from TempData
                if (state != TempData["state"]?.ToString())
                {
                    //return RedirectToAction("Login");
                }

                string codeVerifier = TempData["code_verifier"]?.ToString();

                // Exchange authorization code for access token
                string accessToken = await ExchangeCodeForAccessToken(code, codeVerifier, state);

                //User userModel = new User();

                if (accessToken != null)
                {
                    var userInfo = await GetUserInfoAsync(accessToken);
                    if (userInfo != null)
                    {
                        var userModel = parseUserInfo(userInfo);
                        if (userModel != null && userModel.EsiPin != "")
                        {
                            //регистрирую пользователя
                            //string newUserId = Guid.NewGuid().ToString();
                            string userId = await registrateNewUser(userModel);
                            if (userId != null && userId != "")
                            {
                                //аутентификация
                                userModel.Id = userId;
                                await Authenticate(userModel);
                                _logger.LogInformation("Пользователь " + userModel.Name + " успешно авторизовался через ЕСИ");
                                //await _journalling.AddToJournalAsynk(userModel?.Name, "Авторизация через ЕСИ");
                                return RedirectToAction("Index", "UserPage");
                            }
                        }
                    }
                }



                return RedirectToAction("Index", "Landing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в методе auth");
                ViewBag.errorText = "Не удалось авторизоваться, возникла ошибка: " + ex.Message + ", попробуйте еще раз";
                return View("ErrorPage");
            }
        }

        private PublicUser parseUserInfo(string userInfo)
        {
            try
            {
                PublicUser user = new PublicUser();
                dynamic data = JObject.Parse(userInfo);

                user.EsiSub = data.sub;
                user.EsiEmail = data.email;
                user.EsiPin = data.pin;
                user.EsiPhoneNumber = data.phone_number;
                user.EsiCitizenship = data.citizenship;
                user.EsiFamilyName = data.family_name;
                user.EsiGivenName = data.given_name;
                user.EsiName = data.name;
                user.EsiGender = data.gender;
                user.EsiBirthdate = data.birthdate;
                user.EsiEmailVerified = data.email_verified;
                user.EsiPhoneNumberVerified = data.phone_number_verified;

                user.OrganizationTin = data.organization_tin;
                user.OrganizationName = data.organization_name;

                user.Name = data.name;
                user.Email = data.email;

                return user;
            }
            catch (Exception ex)
            {
                throw new Exception("Не удалось парсить ответ ЕСИ на запрос userInfo");
            }
        }

        private async Task Authenticate(PublicUser user)
        {

            // создаем один claim
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.EsiName),
                new Claim(ClaimTypes.Email, user.EsiEmail),
                new Claim(ClaimTypes.DateOfBirth, user.EsiBirthdate),
                new Claim("pin", user.EsiPin),
                new Claim("orgPin", user.OrganizationTin),
                //new Claim("pin", "01001202210023"), //тест
                new Claim("id", user.Id),
            };
            // создаем объект ClaimsIdentity
            ClaimsIdentity id = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
            // установка аутентификационных куки
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));

        }


        private async Task<string> registrateNewUser(PublicUser user)
        {
            try
            {
                if (_db.PublicUsers.Where(a => a.EsiPin == user.EsiPin) != null && _db.PublicUsers.Where(a => a.EsiPin == user.EsiPin).Count() > 0)
                {
                    return _db.PublicUsers.FirstOrDefault(a => a.EsiPin == user.EsiPin).Id;
                }
                else
                {
                    //новый пользователь 
                    user.Id = Guid.NewGuid().ToString();
                    user.CreatedAt = DateTime.Now;
                    user.UpdatedAt = DateTime.Now;
                    user.Isdeleted = "0";

                    await _db.PublicUsers.AddAsync(user);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Пользователь " + user.Name + ", ИНН: " + user.EsiPin + " успешно зарегистрировался в системе");

                    return user.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в методе registrateNewUser");
                return null;
            }
        }


        public async Task<IActionResult> Logout()
        {
            //logoutEsi();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Landing");
        }


        private async Task<string> logoutEsi()
        {
            using (var client = new HttpClient())
            {

                // Генерация строкового представления Basic Authentication
                string authInfo = _appSettings.clientId + ":" + _appSettings.secretKey;
                authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                string authHeader = "Basic " + authInfo;

                // Добавление заголовка Authorization с Basic Authentication к запросу
                client.DefaultRequestHeaders.Add("Authorization", authHeader);

                var tokenResponse = await client.PostAsync("https://dev-esia.tunduk.kg/connect/token", new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                }));

                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<Dictionary<string, string>>(tokenContent);



                if (tokenData != null && tokenData.ContainsKey("access_token"))
                {
                    return tokenData["access_token"];
                }
                else
                {
                    throw new Exception("Не удалось получить токен от ЕСИ");
                }
            }
        }


        private async Task<string> ExchangeCodeForAccessToken(string code, string codeVerifier, string state)
        {
            try
            {
                using (var client = new HttpClient())
                {

                    // Генерация строкового представления Basic Authentication
                    string authInfo = _appSettings.clientId + ":" + _appSettings.secretKey;
                    authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                    string authHeader = "Basic " + authInfo;

                    // Добавление заголовка Authorization с Basic Authentication к запросу
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);

                    var tokenResponse = await client.PostAsync(_appSettings.tokenUrl, new FormUrlEncodedContent(new[]
                    {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", _appSettings.redirectUrl),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier),
                }));

                    var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                    var tokenData = JsonConvert.DeserializeObject<Dictionary<string, string>>(tokenContent);

                    if (tokenData != null && tokenData.ContainsKey("access_token"))
                    {
                        return tokenData["access_token"];
                    }
                    else
                    {
                        throw new Exception("Не удалось получить токен от ЕСИ");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в методе ExchangeCodeForAccessToken");
                return null;
            }
        }


        private async Task<string> GetUserInfoAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(_appSettings.userInfoUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                // Обработка ошибки получения данных пользователя
                return null;
            }
        }


    }
}
