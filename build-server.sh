#!/bin/bash

# ============================================================================
# Script de Build do Servidor Intersect - Linux
# Compila o servidor e organiza na pasta /server
# ============================================================================

set -e  # Sair em caso de erro

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # Sem cor

# Configurações
VERSION="0.8.0"
PACKAGE_VERSION="0.8.0-beta"
RUNTIME="linux-x64"
CONFIGURATION="Release"
SERVER_PROJECT="Intersect.Server/Intersect.Server.csproj"
OUTPUT_DIR="./server"

echo -e "${BLUE}========================================"
echo "  Build do Servidor Intersect"
echo "========================================${NC}"
echo ""
echo -e "${YELLOW}Versão:${NC} $VERSION"
echo -e "${YELLOW}Runtime:${NC} $RUNTIME"
echo -e "${YELLOW}Configuração:${NC} $CONFIGURATION"
echo ""

# 1. Limpar builds anteriores
echo -e "${BLUE}[1/5] Limpando builds anteriores...${NC}"
if [ -d "$OUTPUT_DIR" ]; then
    rm -rf "$OUTPUT_DIR"
    echo -e "${GREEN}✓${NC} Pasta server limpa"
else
    echo -e "${YELLOW}!${NC} Pasta server não existe (será criada)"
fi

# Limpar bin/obj do projeto
dotnet clean "$SERVER_PROJECT" --configuration "$CONFIGURATION" > /dev/null 2>&1
echo -e "${GREEN}✓${NC} Projeto limpo"
echo ""

# 2. Restaurar dependências
echo -e "${BLUE}[2/5] Restaurando dependências...${NC}"
dotnet restore "$SERVER_PROJECT"
echo -e "${GREEN}✓${NC} Dependências restauradas"
echo ""

# 3. Compilar servidor
echo -e "${BLUE}[3/5] Compilando servidor...${NC}"
dotnet publish "$SERVER_PROJECT" \
    -p:Configuration="$CONFIGURATION" \
    -p:PackageVersion="$PACKAGE_VERSION" \
    -p:Version="$VERSION" \
    -r "$RUNTIME" \
    --self-contained false \
    -o "$OUTPUT_DIR"

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓${NC} Servidor compilado com sucesso!"
else
    echo -e "${RED}✗${NC} Erro ao compilar servidor"
    exit 1
fi
echo ""

# 4. Copiar recursos necessários
echo -e "${BLUE}[4/5] Copiando recursos...${NC}"

# Criar estrutura de pastas
mkdir -p "$OUTPUT_DIR/resources"
mkdir -p "$OUTPUT_DIR/logs"

# Copiar recursos padrão se existirem
if [ -d "Intersect.Server/Resources" ]; then
    cp -r Intersect.Server/Resources/* "$OUTPUT_DIR/resources/" 2>/dev/null || true
    echo -e "${GREEN}✓${NC} Recursos copiados"
fi

# Copiar configurações do Discord se existirem
if [ -f "Intersect.Server/Resources/discord.config.json" ]; then
    cp "Intersect.Server/Resources/discord.config.json" "$OUTPUT_DIR/resources/"
    echo -e "${GREEN}✓${NC} Configuração Discord copiada"
fi

echo ""

# 5. Criar script de inicialização
echo -e "${BLUE}[5/5] Criando script de inicialização...${NC}"

cat > "$OUTPUT_DIR/start-server.sh" << 'STARTSCRIPT'
#!/bin/bash

# ============================================================================
# Script de Inicialização do Servidor Intersect
# ============================================================================

echo "========================================"
echo "  Servidor Intersect"
echo "========================================"
echo ""

# Verificar se o .NET está instalado
if ! command -v dotnet &> /dev/null; then
    echo "ERRO: .NET 8 não está instalado!"
    echo "Instale com: sudo apt install dotnet-sdk-8.0"
    exit 1
fi

# Verificar versão do .NET
DOTNET_VERSION=$(dotnet --version | cut -d'.' -f1)
if [ "$DOTNET_VERSION" -lt 8 ]; then
    echo "ERRO: .NET 8 ou superior é necessário!"
    echo "Versão instalada: $(dotnet --version)"
    exit 1
fi

echo "Iniciando servidor..."
echo ""

# Executar servidor
dotnet Intersect\ Server.dll

# Pausar ao final (útil para ver erros)
read -p "Pressione Enter para fechar..."
STARTSCRIPT

chmod +x "$OUTPUT_DIR/start-server.sh"
echo -e "${GREEN}✓${NC} Script start-server.sh criado"
echo ""

# 6. Informações finais
echo -e "${GREEN}========================================"
echo "  ✓ Build Concluído!"
echo "========================================${NC}"
echo ""
echo -e "${YELLOW}Localização:${NC} $OUTPUT_DIR/"
echo ""
echo -e "${YELLOW}Arquivos gerados:${NC}"
ls -lh "$OUTPUT_DIR" | grep -E "\.dll$|\.sh$" | awk '{print "  • " $9 " (" $5 ")"}'
echo ""
echo -e "${YELLOW}Para iniciar o servidor:${NC}"
echo "  cd $OUTPUT_DIR"
echo "  ./start-server.sh"
echo ""
echo -e "${YELLOW}Ou diretamente:${NC}"
echo "  cd $OUTPUT_DIR"
echo "  dotnet Intersect\\ Server.dll"
echo ""

# Calcular tamanho total
TOTAL_SIZE=$(du -sh "$OUTPUT_DIR" | cut -f1)
echo -e "${BLUE}Tamanho total:${NC} $TOTAL_SIZE"
echo ""
