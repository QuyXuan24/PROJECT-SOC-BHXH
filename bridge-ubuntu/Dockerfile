# Sử dụng Node.js bản 18 (LTS) chạy trên nền Debian/Ubuntu
FROM node:18-bullseye

# Tạo thư mục làm việc trong container
WORKDIR /usr/src/app

# Copy file cấu hình thư viện trước để tối ưu hóa cache Docker
COPY package*.json ./

# Cài đặt thư viện (bao gồm cả build-essential nếu cần biên dịch code Go/C++)
RUN apt-get update && apt-get install -y build-essential python3
RUN npm install

# Copy toàn bộ code nguồn vào container
COPY . .

# Mở cổng 3001 cho máy ảo
EXPOSE 3001

# Lệnh khởi chạy server
CMD [ "node", "server.js" ]