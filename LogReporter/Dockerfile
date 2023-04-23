FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["kaspersky-internship-csharp-2023/kaspersky-internship-csharp-2023.csproj", "kaspersky-internship-csharp-2023/"]
RUN dotnet restore "kaspersky-internship-csharp-2023/kaspersky-internship-csharp-2023.csproj"
COPY . .
WORKDIR "/src/kaspersky-internship-csharp-2023"
RUN dotnet build "kaspersky-internship-csharp-2023.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "kaspersky-internship-csharp-2023.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "kaspersky-internship-csharp-2023.dll"]
