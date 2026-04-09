# --- GIAI ĐOẠN 1: BUILD (Sử dụng SDK để biên dịch code) ---
# Dùng Image SDK 9.0 (LTS - ổn định)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy file project và restore các thư viện (tận dụng Docker cache)
COPY ["BHXH_Backend.csproj", "./"]
RUN dotnet nuget add source "https://mirrors.aliyun.com/nuget/v3/index.json" -n Aliyun --valid-authentication-types basic || true
RUN dotnet nuget add source "https://nuget.cdn.azure.cn/v3/index.json" -n AzureChina --valid-authentication-types basic || true
RUN dotnet restore "BHXH_Backend.csproj"

# Copy toàn bộ source code còn lại và build
COPY . .
RUN dotnet publish "BHXH_Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- GIAI ĐOẠN 2: RUN (Dùng bản Runtime nhẹ để chạy) ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy kết quả build từ giai đoạn 1 sang
COPY --from=build /app/publish .

# Mở cổng 8080 (Cổng mặc định của .NET container)
EXPOSE 8080

# Lệnh chạy ứng dụng
ENTRYPOINT ["dotnet", "BHXH_Backend.dll"]