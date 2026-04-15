using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace data_registry_public.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Incident> Incidents { get; set; }

    public virtual DbSet<IncidentMaterial> IncidentMaterials { get; set; }

    public virtual DbSet<IncidentType> IncidentTypes { get; set; }

    public virtual DbSet<Inspection> Inspections { get; set; }

    public virtual DbSet<InspectionAct> InspectionActs { get; set; }

    public virtual DbSet<InspectionActPrescription> InspectionActPrescriptions { get; set; }

    public virtual DbSet<InspectionActViolation> InspectionActViolations { get; set; }

    public virtual DbSet<InspectionPrecriptionViolation> InspectionPrecriptionViolations { get; set; }

    public virtual DbSet<LocalUser> LocalUsers { get; set; }

    public virtual DbSet<LocalUsersRole> LocalUsersRoles { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationBuisnesSector> OrganizationBuisnesSectors { get; set; }

    public virtual DbSet<PublicUser> PublicUsers { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Sanction> Sanctions { get; set; }

    public virtual DbSet<SanctionMaterial> SanctionMaterials { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=192.168.88.24;Port=5432;Database=dataholder_registry;Username=dataholder_registry;Password=dataholder_registry");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incidents_pk");

            entity.ToTable("incidents");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Actionstaken)
                .HasComment("меры, принятые для устранения инцидента")
                .HasColumnType("character varying")
                .HasColumnName("actionstaken");
            entity.Property(e => e.Affectedinformationsystem)
                .HasComment("затронутая информационная система")
                .HasColumnType("character varying")
                .HasColumnName("affectedinformationsystem");
            entity.Property(e => e.Affectedrecordscount)
                .HasComment("количество затронутых записей")
                .HasColumnName("affectedrecordscount");
            entity.Property(e => e.Aresubjectsnotified)
                .HasComment("факт уведомления субъектов персональных данных")
                .HasColumnName("aresubjectsnotified");
            entity.Property(e => e.Datasubjectcategory)
                .HasComment("Категории субъектов данных:\r\nсотрудники\r\nклиенты\r\nграждане\r\nпользователи сайта\r\nиные категории\r\n")
                .HasColumnType("character varying")
                .HasColumnName("datasubjectcategory");
            entity.Property(e => e.DateCreate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_create");
            entity.Property(e => e.DateDetection)
                .HasComment("дата обнаружения инцедента")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_detection");
            entity.Property(e => e.DateOccurrence)
                .HasComment("дата возникновения инцедента")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_occurrence");
            entity.Property(e => e.Estimateddatasubjectscount)
                .HasComment("примерное количество субъектов персональных данных")
                .HasColumnType("character varying")
                .HasColumnName("estimateddatasubjectscount");
            entity.Property(e => e.Hasriskofharmtosubjects)
                .HasComment("наличие риска причинения вреда субъектам персональных данных")
                .HasColumnName("hasriskofharmtosubjects");
            entity.Property(e => e.IncidentDescription)
                .HasComment("краткое описание инцидента")
                .HasColumnType("character varying")
                .HasColumnName("incident_description");
            entity.Property(e => e.IncidentRegnumber)
                .HasColumnType("character varying")
                .HasColumnName("incident_regnumber");
            entity.Property(e => e.IncidentType)
                .HasComment("Тип инцидента")
                .HasColumnType("character varying")
                .HasColumnName("incident_type");
            entity.Property(e => e.Incidentseverity)
                .HasComment("уровень критичности инцидента:\r\nLow-низкий\r\nMedium-средний\r\nHigh-высокий\r\nCritical-критический")
                .HasColumnType("character varying")
                .HasColumnName("incidentseverity");
            entity.Property(e => e.IncidetLocation)
                .HasComment("место возникновения инцидента (информационная система, подразделение и др.)")
                .HasColumnType("character varying")
                .HasColumnName("incidet_location");
            entity.Property(e => e.Investigationinfo)
                .HasComment("информация о проведении внутреннего расследования")
                .HasColumnType("character varying")
                .HasColumnName("investigationinfo");
            entity.Property(e => e.Isdatacopiedorstolen)
                .HasDefaultValue(false)
                .HasComment("факт копирования или похищения данных")
                .HasColumnName("isdatacopiedorstolen");
            entity.Property(e => e.Isdatapublished)
                .HasComment("факт публикации данных")
                .HasColumnName("isdatapublished");
            entity.Property(e => e.Notificationsentat)
                .HasComment("дата отправки уведомления")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("notificationsentat");
            entity.Property(e => e.Personaldatacategories)
                .HasComment("категории персональных данных которые были затронуты")
                .HasColumnType("character varying")
                .HasColumnName("personaldatacategories");
            entity.Property(e => e.Personaldatatype)
                .HasComment("Regular - обычные\r\nSpecial - специальные\r\nBiometric - биометрические\r\n")
                .HasColumnType("character varying")
                .HasColumnName("personaldatatype");
            entity.Property(e => e.Preventionmeasures)
                .HasComment("меры по предотвращению повторения инцидента")
                .HasColumnType("character varying")
                .HasColumnName("preventionmeasures");
            entity.Property(e => e.Suspectedincidentcause)
                .HasComment("предполагаемая причина инцидента")
                .HasColumnType("character varying")
                .HasColumnName("suspectedincidentcause");
            entity.Property(e => e.Systemtype)
                .HasComment("тип системы:\r\ndb - база данных\r\nwebsite - веб-сайт\r\ninfosystem - информационная система\r\ncloude_service - облачный сервис\r\nlocal_network - локальная сеть")
                .HasColumnType("character varying")
                .HasColumnName("systemtype");

            entity.HasOne(d => d.IncidentTypeNavigation).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.IncidentType)
                .HasConstraintName("incidents_fk");
        });

        modelBuilder.Entity<IncidentMaterial>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incident_materials_pk");

            entity.ToTable("incident_materials", tb => tb.HasComment("скриншоты\r\nжурналы событий (логи)\r\nотчеты\r\nдокументы\r\nиные материалы"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.DateCreate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_create");
            entity.Property(e => e.Incident)
                .HasColumnType("character varying")
                .HasColumnName("incident");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Type)
                .HasColumnType("character varying")
                .HasColumnName("type");

            entity.HasOne(d => d.IncidentNavigation).WithMany(p => p.IncidentMaterials)
                .HasForeignKey(d => d.Incident)
                .HasConstraintName("incident_materials_fk");
        });

        modelBuilder.Entity<IncidentType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incident_types_pk");

            entity.ToTable("incident_types");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
        });

        modelBuilder.Entity<Inspection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inspections_pk");

            entity.ToTable("inspections");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.CheckNumber)
                .HasComment("идентификатор проверки")
                .HasColumnType("character varying")
                .HasColumnName("check_number");
            entity.Property(e => e.Checktype)
                .HasComment("вид проверки")
                .HasColumnType("character varying")
                .HasColumnName("checktype");
            entity.Property(e => e.DateCheckEnd)
                .HasComment("сроки проверки, конец проверки")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_check_end");
            entity.Property(e => e.DateCheckStart)
                .HasComment("сроки проверки, начало проверки")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_check_start");
            entity.Property(e => e.DateCreate)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_create");
            entity.Property(e => e.InspectionCycle)
                .HasDefaultValueSql("1")
                .HasComment("цикл проверки, 1й - цикл это первая проверка и контрольная проверка.\r\n2й цикл это проверка и контрольная проверка в след году к примеру.")
                .HasColumnName("inspection_cycle");
            entity.Property(e => e.InspectionType)
                .HasComment("plan-check - плановая проверка\r\ncontrol-check - контрольная проверка\r\n")
                .HasColumnType("character varying")
                .HasColumnName("inspection_type");
            entity.Property(e => e.Inspector)
                .HasColumnType("character varying")
                .HasColumnName("inspector");
            entity.Property(e => e.Organization)
                .HasComment("проверяемый субъект")
                .HasColumnType("character varying")
                .HasColumnName("organization");
            entity.Property(e => e.PlanCheckInspection)
                .HasComment("ссылка на плановую проверку если данная проверка контрольная проверка")
                .HasColumnType("character varying")
                .HasColumnName("plan-check-inspection");
            entity.Property(e => e.PreCycleControlInspection)
                .HasComment("ссылка на контрольную проверку в предыдущем цикле")
                .HasColumnType("character varying")
                .HasColumnName("pre-cycle-control-inspection");
            entity.Property(e => e.Reasonforcheck)
                .HasComment("основание проведения проверки")
                .HasColumnType("character varying")
                .HasColumnName("reasonforcheck");

            entity.HasOne(d => d.InspectorNavigation).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.Inspector)
                .HasConstraintName("inspections_fk_1");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.Inspections)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("inspections_fk");

            entity.HasOne(d => d.PlanCheckInspectionNavigation).WithMany(p => p.InversePlanCheckInspectionNavigation)
                .HasForeignKey(d => d.PlanCheckInspection)
                .HasConstraintName("inspections_fk_2");

            entity.HasOne(d => d.PreCycleControlInspectionNavigation).WithMany(p => p.InversePreCycleControlInspectionNavigation)
                .HasForeignKey(d => d.PreCycleControlInspection)
                .HasConstraintName("inspections_fk_3");
        });

        modelBuilder.Entity<InspectionAct>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inspectionacts_pk");

            entity.ToTable("inspection_acts");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Actdate)
                .HasComment("дата создание акта")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("actdate");
            entity.Property(e => e.Findings)
                .HasComment("выводы по результатам проверки")
                .HasColumnType("character varying")
                .HasColumnName("findings");
            entity.Property(e => e.Inspection)
                .HasColumnType("character varying")
                .HasColumnName("inspection");

            entity.HasOne(d => d.InspectionNavigation).WithMany(p => p.InspectionActs)
                .HasForeignKey(d => d.Inspection)
                .HasConstraintName("inspection_acts_fk");
        });

        modelBuilder.Entity<InspectionActPrescription>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inspection_act_violations_prescription_pk");

            entity.ToTable("inspection_act_prescription", tb => tb.HasComment("Предписание по акту"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Duedate)
                .HasComment("срок исполнения")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("duedate");
            entity.Property(e => e.Executionstatus)
                .HasComment("статус исполнения:\r\nCompleted-исполнено\n\r\nPartiallyCompleted-частично исполнено\n    NotCompleted-не исполнено.\r\n\r\nт.е если в inspection_precription_violations - все записи по данному предписанию - исполнены, то статус - Completed.\r\nесли есть исполненные и не исполненные то - PartiallyCompleted,\r\nесли все не исполненные то NotCompleted.")
                .HasColumnType("character varying")
                .HasColumnName("executionstatus");
            entity.Property(e => e.PrescriptionNumber)
                .HasComment("номер предписания")
                .HasColumnType("character varying")
                .HasColumnName("prescription_number");
            entity.Property(e => e.Violations)
                .HasComment("по каким нарушениям предписание. \r\nперечисляется id нарушений через точку с запятой")
                .HasColumnType("character varying")
                .HasColumnName("violations");
        });

        modelBuilder.Entity<InspectionActViolation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inspection_act_violations_pk");

            entity.ToTable("inspection_act_violations", tb => tb.HasComment("нарушения"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Act)
                .HasComment("нарушения по акту")
                .HasColumnType("character varying")
                .HasColumnName("act");
            entity.Property(e => e.Violatedlegalnorms)
                .HasComment("нарушенные нормы законодательства")
                .HasColumnType("character varying")
                .HasColumnName("violatedlegalnorms");
            entity.Property(e => e.ViolationText)
                .HasComment("нарушение")
                .HasColumnType("character varying")
                .HasColumnName("violation_text");
            entity.Property(e => e.Violationcause)
                .HasComment("причины нарушений(через точку с запятой):")
                .HasColumnType("character varying")
                .HasColumnName("violationcause");

            entity.HasOne(d => d.ActNavigation).WithMany(p => p.InspectionActViolations)
                .HasForeignKey(d => d.Act)
                .HasConstraintName("inspection_act_violations_fk");
        });

        modelBuilder.Entity<InspectionPrecriptionViolation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inspection_precription_violations_pk");

            entity.ToTable("inspection_precription_violations", tb => tb.HasComment("по каким нарушениям акта предписания. т.е для связи многие ко многим"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Executionstatus)
                .HasComment("статус исполнения:\r\nCompleted-исполнено\n\r\nNotCompleted-не исполнено")
                .HasColumnType("character varying")
                .HasColumnName("executionstatus");
            entity.Property(e => e.Precription)
                .HasComment("предписание")
                .HasColumnType("character varying")
                .HasColumnName("precription");
            entity.Property(e => e.Violation)
                .HasComment("нарушение")
                .HasColumnType("character varying")
                .HasColumnName("violation");

            entity.HasOne(d => d.PrecriptionNavigation).WithMany(p => p.InspectionPrecriptionViolations)
                .HasForeignKey(d => d.Precription)
                .HasConstraintName("inspection_precription_violations_fk");

            entity.HasOne(d => d.ViolationNavigation).WithMany(p => p.InspectionPrecriptionViolations)
                .HasForeignKey(d => d.Violation)
                .HasConstraintName("inspection_precription_violations_fk_1");
        });

        modelBuilder.Entity<LocalUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("local_users_pk");

            entity.ToTable("local_users");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.Login)
                .HasComment("if local_user")
                .HasColumnType("character varying")
                .HasColumnName("login");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Password)
                .HasComment("if local_user")
                .HasColumnType("character varying")
                .HasColumnName("password");
            entity.Property(e => e.UserType)
                .HasComment("local\r\nactive_directory")
                .HasColumnType("character varying")
                .HasColumnName("user_type");
        });

        modelBuilder.Entity<LocalUsersRole>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("local_users_roles");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Role)
                .HasColumnType("character varying")
                .HasColumnName("role");
            entity.Property(e => e.User)
                .HasColumnType("character varying")
                .HasColumnName("user");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organizations_pk");

            entity.ToTable("organizations", tb => tb.HasComment("субьект, организация"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Businesssector)
                .HasComment("сфера деятельности субъекта:\r\nGovernment — государственные органы\r\nTelecommunications — телеком\r\nHealthcare — медицина\r\nBanking — банки / финансы\r\nEducation — образование\r\nInsurance — страхование\r\nRetail — торговля\r\nIT — IT / технологии\r\nManufacturing — производство\r\nEnergy — энергетика\r\nTransportation — транспорт / логистика\r\nAgriculture — сельское хозяйство\r\nMedia — СМИ\r\nHospitality — туризм / гостиницы\r\nOther — другое")
                .HasColumnType("character varying")
                .HasColumnName("businesssector");
            entity.Property(e => e.Fullnamegl)
                .HasColumnType("character varying")
                .HasColumnName("fullnamegl");
            entity.Property(e => e.Risklevel)
                .HasComment("уровень риска субъекта:\r\nLow-низкий\n\r\nMedium-средний\n\r\nHigh-высокий\n\r\nCritical-критический")
                .HasColumnType("character varying")
                .HasColumnName("risklevel");

            entity.HasOne(d => d.BusinesssectorNavigation).WithMany(p => p.Organizations)
                .HasForeignKey(d => d.Businesssector)
                .HasConstraintName("organizations_fk");
        });

        modelBuilder.Entity<OrganizationBuisnesSector>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("organization_buisnes_sectors_pk");

            entity.ToTable("organization_buisnes_sectors");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.SectorEn)
                .HasColumnType("character varying")
                .HasColumnName("sector_en");
            entity.Property(e => e.SectorKg)
                .HasColumnType("character varying")
                .HasColumnName("sector_kg");
            entity.Property(e => e.SectorRu)
                .HasColumnType("character varying")
                .HasColumnName("sector_ru");
        });

        modelBuilder.Entity<PublicUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("public_users_pk");

            entity.ToTable("public_users", tb => tb.HasComment("пользователи через ЕСИ"));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.AccessToken).HasColumnName("access_token");
            entity.Property(e => e.Active)
                .HasDefaultValue(true)
                .HasColumnName("active");
            entity.Property(e => e.Archived)
                .HasPrecision(18, 2)
                .HasColumnName("archived");
            entity.Property(e => e.CommentArchive).HasColumnName("comment_archive");
            entity.Property(e => e.CommentDeArchive).HasColumnName("comment_de_archive");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("deleted_at");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.EmailVerifiedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("email_verified_at");
            entity.Property(e => e.EsiBirthdate).HasColumnName("esi_birthdate");
            entity.Property(e => e.EsiCitizenship).HasColumnName("esi_citizenship");
            entity.Property(e => e.EsiEmail).HasColumnName("esi_email");
            entity.Property(e => e.EsiEmailVerified).HasColumnName("esi_email_verified");
            entity.Property(e => e.EsiFamilyName).HasColumnName("esi_family_name");
            entity.Property(e => e.EsiGender).HasColumnName("esi_gender");
            entity.Property(e => e.EsiGivenName).HasColumnName("esi_given_name");
            entity.Property(e => e.EsiName).HasColumnName("esi_name");
            entity.Property(e => e.EsiPhoneNumber).HasColumnName("esi_phone_number");
            entity.Property(e => e.EsiPhoneNumberVerified).HasColumnName("esi_phone_number_verified");
            entity.Property(e => e.EsiPin).HasColumnName("esi_pin");
            entity.Property(e => e.EsiSub).HasColumnName("esi_sub");
            entity.Property(e => e.ExpiresIn).HasColumnName("expires_in");
            entity.Property(e => e.IdToken).HasColumnName("id_token");
            entity.Property(e => e.Isdeleted).HasColumnName("isdeleted");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Organization)
                .HasColumnType("character varying")
                .HasColumnName("organization");
            entity.Property(e => e.OrganizationName).HasColumnName("organization_name");
            entity.Property(e => e.OrganizationTin).HasColumnName("organization_tin");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.PositionName).HasColumnName("position_name");
            entity.Property(e => e.RegisterId).HasColumnName("register_id");
            entity.Property(e => e.RememberToken).HasColumnName("remember_token");
            entity.Property(e => e.Scope).HasColumnName("scope");
            entity.Property(e => e.TelegramId).HasColumnName("telegram_id");
            entity.Property(e => e.TokenType).HasColumnName("token_type");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.OrganizationNavigation).WithMany(p => p.PublicUsers)
                .HasForeignKey(d => d.Organization)
                .HasConstraintName("public_users_fk");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pk");

            entity.ToTable("roles");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
        });

        modelBuilder.Entity<Sanction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sanctions_pk");

            entity.ToTable("sanctions");

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.Appliedsanctions)
                .HasComment("примененные меры ответственности")
                .HasColumnType("character varying")
                .HasColumnName("appliedsanctions");
            entity.Property(e => e.DateExecuted)
                .HasComment("дата исполения")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_executed");
            entity.Property(e => e.DateReg)
                .HasComment("дата составления протокола")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("date_reg");
            entity.Property(e => e.Executedofficer)
                .HasComment("исполнивший сотрудник")
                .HasColumnType("character varying")
                .HasColumnName("executedofficer");
            entity.Property(e => e.Executionreason)
                .HasComment("основание на исполнение")
                .HasColumnType("character varying")
                .HasColumnName("executionreason");
            entity.Property(e => e.Executionstatus)
                .HasComment("Completed-исполнено\r\nNotCompleted-не исполнено\n\r\nAppealed-обжалуется\n   UnderJudicialProceeding-находится в судебном производстве")
                .HasColumnType("character varying")
                .HasColumnName("executionstatus");
            entity.Property(e => e.Fineamount)
                .HasComment("размер наложенного штрафа")
                .HasColumnName("fineamount");
            entity.Property(e => e.InspectionAct)
                .HasComment("по какому акту - протокол")
                .HasColumnType("character varying")
                .HasColumnName("inspection_act");
            entity.Property(e => e.InspectionActPrescription)
                .HasComment("по какому предписанию - протокол")
                .HasColumnType("character varying")
                .HasColumnName("inspection_act_prescription");
            entity.Property(e => e.Paidamount)
                .HasComment("оплаченная сумма")
                .HasColumnType("character varying")
                .HasColumnName("paidamount");
            entity.Property(e => e.PreSanction)
                .HasComment("ссылка не предыдущий протокол если повтроно составляется протокол с новой суммой")
                .HasColumnType("character varying")
                .HasColumnName("pre-sanction");
            entity.Property(e => e.Protocolofficer)
                .HasColumnType("character varying")
                .HasColumnName("protocolofficer");
            entity.Property(e => e.Reasonforliability)
                .HasComment("основание привлечения")
                .HasColumnType("character varying")
                .HasColumnName("reasonforliability");
            entity.Property(e => e.Violations)
                .HasComment("не исполненные предписания по нарушениям. \r\nid из inspection_precription_violations через точку с запятой")
                .HasColumnType("character varying")
                .HasColumnName("violations");

            entity.HasOne(d => d.ExecutedofficerNavigation).WithMany(p => p.SanctionExecutedofficerNavigations)
                .HasForeignKey(d => d.Executedofficer)
                .HasConstraintName("sanctions_fk_3");

            entity.HasOne(d => d.InspectionActNavigation).WithMany(p => p.Sanctions)
                .HasForeignKey(d => d.InspectionAct)
                .HasConstraintName("sanctions_fk");

            entity.HasOne(d => d.InspectionActPrescriptionNavigation).WithMany(p => p.Sanctions)
                .HasForeignKey(d => d.InspectionActPrescription)
                .HasConstraintName("sanctions_fk_1");

            entity.HasOne(d => d.PreSanctionNavigation).WithMany(p => p.InversePreSanctionNavigation)
                .HasForeignKey(d => d.PreSanction)
                .HasConstraintName("sanctions_fk_4");

            entity.HasOne(d => d.ProtocolofficerNavigation).WithMany(p => p.SanctionProtocolofficerNavigations)
                .HasForeignKey(d => d.Protocolofficer)
                .HasConstraintName("sanctions_fk_2");
        });

        modelBuilder.Entity<SanctionMaterial>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sanction_materials_pk");

            entity.ToTable("sanction_materials", tb => tb.HasComment("Документы:\r\nпротоколы;\r\nпостановления;\r\nсудебные акты;\r\nиные подтверждающие материалы."));

            entity.Property(e => e.Id)
                .HasColumnType("character varying")
                .HasColumnName("id");
            entity.Property(e => e.DateCreate)
                .HasColumnType("character varying")
                .HasColumnName("date_create");
            entity.Property(e => e.FormatFile)
                .HasComment("формат файла - docx, xlsx итд")
                .HasColumnType("character varying")
                .HasColumnName("format_file");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Sanction)
                .HasColumnType("character varying")
                .HasColumnName("sanction");
            entity.Property(e => e.TypeFile)
                .HasComment("Протокол — Protocol\r\n\r\r\nПостановления — Resolutions\r\n\r\r\nСудебные акты — JudicialActs\r\n\r\r\nИные материалы — OtherMaterials")
                .HasColumnType("character varying")
                .HasColumnName("type_file");

            entity.HasOne(d => d.SanctionNavigation).WithMany(p => p.SanctionMaterials)
                .HasForeignKey(d => d.Sanction)
                .HasConstraintName("sanction_materials_fk");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
