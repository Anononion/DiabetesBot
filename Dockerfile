FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
# 👇 ВАЖНО: копируем Data В ОБРАЗ
COPY Data ./Data

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out .
# 👇 И сюда тоже
COPY --from=build /app/Data ./Data

ENTRYPOINT ["dotnet", "DiabetesBot.dll"]
