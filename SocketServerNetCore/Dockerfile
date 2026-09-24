FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SocketPlayground.sln ./
COPY CommonClassLibrary/CommonClassLibrary.csproj CommonClassLibrary/
COPY SocketServerNetCore/SocketServerNetCore.csproj SocketServerNetCore/
COPY SocketServerNetCore.Tests/SocketServerNetCore.Tests.csproj SocketServerNetCore.Tests/
COPY CommonLibTest/CommonLibTest.csproj CommonLibTest/
COPY DLLLibrary/DLLLibrary.csproj DLLLibrary/
COPY PayloadNetNode/PayloadNetNode.csproj PayloadNetNode/
COPY WatchDogNetCore/WatchDogNetCore.csproj WatchDogNetCore/
RUN dotnet restore SocketServerNetCore/SocketServerNetCore.csproj
COPY CommonClassLibrary/ CommonClassLibrary/
COPY SocketServerNetCore/ SocketServerNetCore/
RUN dotnet publish SocketServerNetCore/SocketServerNetCore.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV SOCKET_PLAYGROUND_AUTH_SECRET=change-this-local-dev-secret
EXPOSE 11000
ENTRYPOINT ["dotnet", "SocketServerNetCore.dll"]
CMD ["server", "--port", "11000", "--bind", "0.0.0.0"]
