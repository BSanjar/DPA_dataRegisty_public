using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace data_registry_public.Models.local_models
{
    /// <summary>
    /// Реквизиты организации, которые представитель указывает вручную, когда Минюст не нашёл ИНН из ЕСИ.
    /// ИНН сюда не входит: он берётся только из сессии (ЕСИ), чтобы его нельзя было подменить со страницы.
    /// Те же правила проверяются на странице (wwwroot/js/org-details.js).
    /// </summary>
    public class OrganizationDetailsInput
    {
        /// <summary>Организационно-правовые формы (коды хранятся в organizations.legal_form как есть).</summary>
        public static readonly IReadOnlyList<string> LegalForms = new[]
        {
            "ОсОО", "ОАО", "ЗАО", "ГП", "МП", "Учреждение", "ОО", "ОФ", "Кооператив",
            "Полное товарищество", "Коммандитное товарищество", "Филиал", "Представительство", "Другое"
        };

        public const string DateFormat = "dd.MM.yyyy";
        public static readonly DateTime MinRegistrationDate = new(1991, 1, 1);

        // Буквы (кириллица, кыргызские Ө Ү Ң, латиница), пробел, дефис, апостроф.
        public const string PersonNamePattern = @"^[A-Za-zА-Яа-яЁёӨөҮүҢң][A-Za-zА-Яа-яЁёӨөҮүҢң'’\- ]*$";
        // Буквы, цифры, дефис, косая черта, точка; хотя бы одна цифра.
        public const string RegNumberPattern = @"^(?=.*\d)[0-9A-Za-zА-Яа-яЁёӨөҮүҢң\-/.]+$";

        [Required(ErrorMessage = "Укажите полное наименование")]
        [StringLength(500, MinimumLength = 3, ErrorMessage = "От 3 до 500 символов")]
        public string? FullName { get; set; }

        [StringLength(200, ErrorMessage = "Не длиннее 200 символов")]
        public string? ShortName { get; set; }

        [Required(ErrorMessage = "Выберите организационно-правовую форму")]
        [OneOfLegalForms]
        public string? LegalForm { get; set; }

        [Required(ErrorMessage = "Выберите сферу деятельности")]
        public string? BusinessSector { get; set; }

        [Required(ErrorMessage = "Укажите регистрационный номер")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "От 3 до 50 символов")]
        [RegularExpression(RegNumberPattern, ErrorMessage = "Буквы, цифры, «-», «/», «.»; хотя бы одна цифра")]
        public string? RegistrationNumber { get; set; }

        [Required(ErrorMessage = "Укажите дату регистрации")]
        [DataType(DataType.Date)]
        [RegistrationDateRange]
        public DateTime? RegistrationDate { get; set; }

        [Required(ErrorMessage = "Укажите юридический адрес")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "От 10 до 500 символов")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Укажите ФИО руководителя")]
        [StringLength(200, ErrorMessage = "Не длиннее 200 символов")]
        [RegularExpression(PersonNamePattern, ErrorMessage = "Только буквы, пробел, дефис и апостроф")]
        [MinWords(2, ErrorMessage = "Укажите фамилию и имя")]
        public string? DirectorName { get; set; }

        [Required(ErrorMessage = "Укажите должность руководителя")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "От 2 до 200 символов")]
        public string? DirectorPosition { get; set; }

        /// <summary>Приводит значения к виду для хранения: обрезка, одиночные пробелы, рег. номер — заглавными.</summary>
        public void Normalize()
        {
            FullName = Squash(FullName);
            ShortName = Squash(ShortName);
            LegalForm = Squash(LegalForm);
            BusinessSector = Squash(BusinessSector);
            RegistrationNumber = Squash(RegistrationNumber)?.ToUpperInvariant();
            Address = Squash(Address);
            DirectorName = Squash(DirectorName);
            DirectorPosition = Squash(DirectorPosition);
        }

        /// <summary>Дата регистрации в формате хранения (как отдаёт Минюст).</summary>
        public string? RegistrationDateText => RegistrationDate?.ToString(DateFormat, CultureInfo.InvariantCulture);

        /// <summary>Разбор даты из строки organizations.registration_date (dd.MM.yyyy или ISO).</summary>
        public static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var formats = new[] { DateFormat, "yyyy-MM-dd", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" };
            return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.Date
                : null;
        }

        private static string? Squash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return Regex.Replace(value.Trim(), @"\s+", " ");
        }
    }

    /// <summary>Значение из <see cref="OrganizationDetailsInput.LegalForms"/>.</summary>
    public sealed class OneOfLegalFormsAttribute : ValidationAttribute
    {
        public OneOfLegalFormsAttribute() : base("Выберите форму из списка") { }

        public override bool IsValid(object? value) =>
            value is not string s || string.IsNullOrWhiteSpace(s) || OrganizationDetailsInput.LegalForms.Contains(s.Trim());
    }

    /// <summary>Дата регистрации: не в будущем и не раньше <see cref="OrganizationDetailsInput.MinRegistrationDate"/>.</summary>
    public sealed class RegistrationDateRangeAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            if (value is not DateTime date) return ValidationResult.Success;
            var members = context.MemberName is null ? null : new[] { context.MemberName };
            if (date.Date > DateTime.Today) return new ValidationResult("Дата не может быть в будущем", members);
            if (date.Date < OrganizationDetailsInput.MinRegistrationDate) return new ValidationResult("Дата не раньше 01.01.1991", members);
            return ValidationResult.Success;
        }
    }

    /// <summary>Не меньше N слов (ФИО: фамилия и имя).</summary>
    public sealed class MinWordsAttribute : ValidationAttribute
    {
        public int Count { get; }

        public MinWordsAttribute(int count) { Count = count; }

        public override bool IsValid(object? value) =>
            value is not string s || string.IsNullOrWhiteSpace(s)
            || s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= Count;
    }
}
