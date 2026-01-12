@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

REM ============================================================================
REM Script de Build do Servidor Intersect - Windows
REM Compila o servidor e organiza na pasta /server
REM ============================================================================

title Build do Servidor Intersect

REM Configurações
set VERSION=0.8.0
set PACKAGE_VERSION=0.8.0-beta
set RUNTIME=linux-x64
set CONFIGURATION=Release
set SERVER_PROJECT=Intersect.Server\Intersect.Server.csproj
set OUTPUT_DIR=.\server

echo ========================================
echo   Build do Servidor Intersect
echo ========================================
echo.
echo Versão: %VERSION%
echo Runtime: %RUNTIME%
echo Configuração: %CONFIGURATION%
echo.

REM 1. Limpar builds anteriores
echo [1/5] Limpando builds anteriores...
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%" 2>nul
    echo ✓ Pasta server limpa
) else (
    echo ! Pasta server não existe (será criada^)
)

REM Limpar bin/obj do projeto
dotnet clean "%SERVER_PROJECT%" --configuration "%CONFIGURATION%" >nul 2>&1
echo ✓ Projeto limpo
echo.

REM 2. Restaurar dependências
echo [2/5] Restaurando dependências...
dotnet restore "%SERVER_PROJECT%"
if errorlevel 1 (
    echo ✗ Erro ao restaurar dependências
    pause
    exit /b 1
)
echo ✓ Dependências restauradas
echo.

REM 3. Compilar servidor
echo [3/5] Compilando servidor...
dotnet publish "%SERVER_PROJECT%" ^
    -p:Configuration="%CONFIGURATION%" ^
    -p:PackageVersion="%PACKAGE_VERSION%" ^
    -p:Version="%VERSION%" ^
    -r "%RUNTIME%" ^
    --self-contained false ^
    -o "%OUTPUT_DIR%"

if errorlevel 1 (
    echo ✗ Erro ao compilar servidor
    pause
    exit /b 1
)
echo ✓ Servidor compilado com sucesso!
echo.

REM 4. Copiar recursos necessários
echo [4/5] Copiando recursos...

REM Criar estrutura de pastas
if not exist "%OUTPUT_DIR%\resources" mkdir "%OUTPUT_DIR%\resources"
if not exist "%OUTPUT_DIR%\logs" mkdir "%OUTPUT_DIR%\logs"

REM Copiar recursos padrão se existirem
if exist "Intersect.Server\Resources" (
    xcopy /E /I /Y /Q "Intersect.Server\Resources\*" "%OUTPUT_DIR%\resources\" >nul 2>&1
    echo ✓ Recursos copiados
)

REM Copiar configurações do Discord se existirem
if exist "Intersect.Server\Resources\discord.config.json" (
    copy /Y "Intersect.Server\Resources\discord.config.json" "%OUTPUT_DIR%\resources\" >nul
    echo ✓ Configuração Discord copiada
)
echo.

REM 5. Criar script de inicialização
echo [5/5] Criando scripts de inicialização...

REM Script Windows
echo @echo off > "%OUTPUT_DIR%\start-server.bat"
echo chcp 65001 ^>nul >> "%OUTPUT_DIR%\start-server.bat"
echo title Servidor Intersect >> "%OUTPUT_DIR%\start-server.bat"
echo. >> "%OUTPUT_DIR%\start-server.bat"
echo echo ======================================== >> "%OUTPUT_DIR%\start-server.bat"
echo echo   Servidor Intersect >> "%OUTPUT_DIR%\start-server.bat"
echo echo ======================================== >> "%OUTPUT_DIR%\start-server.bat"
echo echo. >> "%OUTPUT_DIR%\start-server.bat"
echo. >> "%OUTPUT_DIR%\start-server.bat"
echo REM Verificar se o .NET está instalado >> "%OUTPUT_DIR%\start-server.bat"
echo where dotnet ^>nul 2^>nul >> "%OUTPUT_DIR%\start-server.bat"
echo if errorlevel 1 ( >> "%OUTPUT_DIR%\start-server.bat"
echo     echo ERRO: .NET 8 não está instalado! >> "%OUTPUT_DIR%\start-server.bat"
echo     echo Baixe em: https://dotnet.microsoft.com/download/dotnet/8.0 >> "%OUTPUT_DIR%\start-server.bat"
echo     pause >> "%OUTPUT_DIR%\start-server.bat"
echo     exit /b 1 >> "%OUTPUT_DIR%\start-server.bat"
echo ^) >> "%OUTPUT_DIR%\start-server.bat"
echo. >> "%OUTPUT_DIR%\start-server.bat"
echo echo Iniciando servidor... >> "%OUTPUT_DIR%\start-server.bat"
echo echo. >> "%OUTPUT_DIR%\start-server.bat"
echo. >> "%OUTPUT_DIR%\start-server.bat"
echo REM Executar servidor >> "%OUTPUT_DIR%\start-server.bat"
echo dotnet "Intersect Server.dll" >> "%OUTPUT_DIR%\start-server.bat"
echo. >> "%OUTPUT_DIR%\start-server.bat"
echo REM Pausar ao final (útil para ver erros^) >> "%OUTPUT_DIR%\start-server.bat"
echo pause >> "%OUTPUT_DIR%\start-server.bat"

echo ✓ Script start-server.bat criado

REM Script Linux (se for deploy cross-platform)
echo #!/bin/bash > "%OUTPUT_DIR%\start-server.sh"
echo. >> "%OUTPUT_DIR%\start-server.sh"
echo echo "========================================" >> "%OUTPUT_DIR%\start-server.sh"
echo echo "  Servidor Intersect" >> "%OUTPUT_DIR%\start-server.sh"
echo echo "========================================" >> "%OUTPUT_DIR%\start-server.sh"
echo echo "" >> "%OUTPUT_DIR%\start-server.sh"
echo. >> "%OUTPUT_DIR%\start-server.sh"
echo if ! command -v dotnet ^&^> /dev/null; then >> "%OUTPUT_DIR%\start-server.sh"
echo     echo "ERRO: .NET 8 não está instalado!" >> "%OUTPUT_DIR%\start-server.sh"
echo     echo "Instale com: sudo apt install dotnet-sdk-8.0" >> "%OUTPUT_DIR%\start-server.sh"
echo     exit 1 >> "%OUTPUT_DIR%\start-server.sh"
echo fi >> "%OUTPUT_DIR%\start-server.sh"
echo. >> "%OUTPUT_DIR%\start-server.sh"
echo echo "Iniciando servidor..." >> "%OUTPUT_DIR%\start-server.sh"
echo echo "" >> "%OUTPUT_DIR%\start-server.sh"
echo. >> "%OUTPUT_DIR%\start-server.sh"
echo dotnet Intersect\ Server.dll >> "%OUTPUT_DIR%\start-server.sh"
echo. >> "%OUTPUT_DIR%\start-server.sh"
echo read -p "Pressione Enter para fechar..." >> "%OUTPUT_DIR%\start-server.sh"

echo ✓ Script start-server.sh criado
echo.

REM 6. Informações finais
echo ========================================
echo   ✓ Build Concluído!
echo ========================================
echo.
echo Localização: %OUTPUT_DIR%\
echo.
echo Arquivos gerados:
dir /b "%OUTPUT_DIR%\*.dll" "%OUTPUT_DIR%\*.bat" 2>nul | findstr /R "." >nul && (
    for %%F in ("%OUTPUT_DIR%\*.dll" "%OUTPUT_DIR%\*.bat") do (
        if exist "%%F" echo   • %%~nxF
    )
)
echo.
echo Para iniciar o servidor:
echo   cd %OUTPUT_DIR%
echo   start-server.bat
echo.
echo Ou diretamente:
echo   cd %OUTPUT_DIR%
echo   dotnet "Intersect Server.dll"
echo.

REM Calcular tamanho total
for /f "tokens=3" %%a in ('dir "%OUTPUT_DIR%" ^| find "bytes"') do set SIZE=%%a
echo Tamanho total: %SIZE% bytes
echo.

pause
