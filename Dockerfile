# =========================
# BUILD STAGE
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем CSPROJ и восстанавливаем зависимости
COPY DiabetesBot.csproj ./
RUN dotnet restore

# Копируем ВСЁ
COPY . ./

# Перекидываем Data В BUILDER
COPY Data /src/Data

# Публикуем
RUN dotnet publish -c Release -o /app/out


# =========================
# RUNTIME STAGE
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Копируем опубликованные файлы
COPY --from=build /app/out ./

# Копируем Data ТАКЖЕ В РАНТАЙМ
COPY --from=build /src/Data ./Data

ENTRYPOINT ["dotnet", "DiabetesBot.dll"]
