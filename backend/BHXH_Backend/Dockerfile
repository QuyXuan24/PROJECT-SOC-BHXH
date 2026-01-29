# --- GIAI ĐOẠN 1: BUILD (Sử dụng SDK để biên dịch code) ---
# Dùng Image SDK 10.0 để khớp với version trên máy bạn
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy file project và restore các thư viện (tận dụng Docker cache)
COPY ["BHXH_Backend.csproj", "./"]
RUN dotnet restore "BHXH_Backend.csproj"

# Copy toàn bộ source code còn lại và build
COPY . .
RUN dotnet publish "BHXH_Backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- GIAI ĐOẠN 2: RUN (Dùng bản Runtime nhẹ để chạy) ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Copy kết quả build từ giai đoạn 1 sang
COPY --from=build /app/publish .

# Mở cổng 8080 (Cổng mặc định của .NET container)
EXPOSE 8080

# Lệnh chạy ứng dụng
ENTRYPOINT ["dotnet", "BHXH_Backend.dll"]