# 使用官方.NET SDK镜像作为构建环境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件并恢复依赖
COPY *.csproj ./
RUN dotnet restore

# 复制所有源代码并构建项目
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# 使用运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 安装一些常用的诊断工具（可选）
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    curl \
    procps \
    && rm -rf /var/lib/apt/lists/*

# 从构建阶段复制发布的应用
COPY --from=build /app/publish .
RUN chown 1000:100 /app -R
USER 1000:100


# 设置环境变量
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV JELLYFIN_URL=http://jellyfin:8096 \
    JELLYFIN_CONFIG_DIR=/config \
    MEILI_URL=http://meilisearch:7700

EXPOSE 5000

# 设置入口点
ENTRYPOINT ["dotnet", "jellysearch.dll"]

