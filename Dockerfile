# -------------------------------------------------
# 1) Build stage
# -------------------------------------------------
FROM mcr.microsoft.comdotnetsdk8.0 AS build

WORKDIR app

# копируем .csproj
COPY .csproj .
COPY DiabetesBot .DiabetesBot

RUN dotnet restore .DiabetesBotDiabetesBot.csproj

# копируем весь проект
COPY . .

RUN dotnet publish .DiabetesBotDiabetesBot.csproj -c Release -o apppublish

# -------------------------------------------------
# 2) Runtime stage
# -------------------------------------------------
FROM mcr.microsoft.comdotnetruntime8.0

WORKDIR app

COPY --from=build apppublish .

# Render установит PORT, но Telegram polling его игнорирует. Но переменная нужна.
ENV ASPNETCORE_URLS=http+$PORT

# Запуск приложения
ENTRYPOINT [dotnet, DiabetesBot.dll]
