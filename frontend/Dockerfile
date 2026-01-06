FROM node:20-alpine
WORKDIR /usr/src/app
# Cài đặt serve toàn cục trong container
RUN npm install -g serve
COPY package*.json ./
RUN npm install
COPY . .
EXPOSE 3000
# Lệnh chạy frontend ổn định
CMD ["serve", "-s", "src", "-l", "3000"]