# См. статью по ссылке https://aka.ms/customizecontainer, чтобы узнать как настроить контейнер отладки и как Visual Studio использует этот Dockerfile для создания образов для ускорения отладки.

# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["data_registry_public.csproj", "."]
RUN dotnet restore "./data_registry_public.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./data_registry_public.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./data_registry_public.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Логи (Serilog): /logs/registry_public/{auth,integration,system,errors}/*_yyyyMMdd.log, хранятся 90 дней.
# Приложение работает под $APP_UID — папку создаём заранее и отдаём ему.
# Монтировать общий для обеих частей каталог /logs, иначе логи пропадут при пересоздании контейнера:
#   -v /logs:/logs   → на хосте /logs/registry_public и /logs/registry_local
# Время в логах и в приложении — бишкекское (TZ), а не UTC контейнера.
USER root
RUN mkdir -p /logs/registry_public && chown -R $APP_UID /logs
USER $APP_UID
ENV TZ=Asia/Bishkek
ENV Logging__Directory=/logs/registry_public
VOLUME ["/logs"]
ENTRYPOINT ["dotnet", "data_registry_public.dll"]