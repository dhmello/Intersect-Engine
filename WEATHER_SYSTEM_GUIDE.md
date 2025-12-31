# Sistema de Chuva Global - Guia de Uso

## 🌧️ Visão Geral

O sistema de chuva global permite que você controle o clima sincronizado em todo o servidor. Todos os jogadores verão o mesmo clima ao mesmo tempo, criando uma experiência mais imersiva.

## 📋 Como Funciona

- **Mapas Outdoor** (`IsIndoors = false`): Mostram o clima **GLOBAL** sincronizado pelo servidor
- **Mapas Indoor** (`IsIndoors = true`): Mostram o clima **LOCAL** configurado no editor de mapas

## 🎮 Comandos do Servidor

### Ativar Clima Global

```bash
weather <animation-id> [xspeed] [yspeed] [intensity]
```

**Parâmetros:**
- `animation-id`: ID (GUID) da animação de clima
- `xspeed`: Velocidade horizontal (padrão: 2)
- `yspeed`: Velocidade vertical (padrão: 3)
- `intensity`: Intensidade de 0-100 (padrão: 50)

**Exemplos:**

```bash
# Chuva com sua animação
weather dcd0472c-264b-4e8f-9250-065fd54460c2

# Chuva customizada
weather dcd0472c-264b-4e8f-9250-065fd54460c2 3 5 70

# Neve leve
weather <snow-animation-id> 1 2 30

# Tempestade intensa
weather dcd0472c-264b-4e8f-9250-065fd54460c2 5 5 100
```

### Limpar Clima Global

```bash
weather clear
```

ou

```bash
weather none
```

ou

```bash
weather off
```

## 💻 Uso via Código (Events/Scripts)

```csharp
// Importar namespace
using Intersect.Server.General;
using Intersect.Server.Networking;

// Ativar chuva
Guid rainAnimId = Guid.Parse("dcd0472c-264b-4e8f-9250-065fd54460c2");
Weather.SetWeather(rainAnimId, 2, 3, 50);
PacketSender.SendGlobalWeatherToAll();

// Limpar clima
Weather.SetWeather(Guid.Empty, 0, 0, 0);
PacketSender.SendGlobalWeatherToAll();

// Obter clima atual
var currentAnimId = Weather.GetWeatherAnimationId();
var xSpeed = Weather.GetWeatherXSpeed();
var ySpeed = Weather.GetWeatherYSpeed();
var intensity = Weather.GetWeatherIntensity();
```

## 🎨 Configurando Animações de Clima

1. **Abra o Editor de Jogo**
2. **Vá para Animações**
3. **Crie uma nova animação** para seu clima (chuva, neve, etc.)
4. **Anote o ID da animação** (você pode ver no editor)
5. **Use esse ID** nos comandos

## 🗺️ Configurando Mapas

### Mapas Outdoor (usam clima global)
1. Abra o mapa no editor
2. Propriedades do Mapa
3. **Desmarque** "Is Indoors"

### Mapas Indoor (usam clima local)
1. Abra o mapa no editor
2. Propriedades do Mapa
3. **Marque** "Is Indoors"
4. Configure o clima local (Weather Animation, X/Y Speed, Intensity)

## 🔧 Arquitetura do Sistema

### Servidor
- `Intersect.Server.Core/General/Weather.cs` - Gerenciador de clima global
- `Intersect.Server/Core/Commands/WeatherCommand.cs` - Comando administrativo
- `Intersect.Server.Core/Networking/PacketSender.cs` - Envio de dados

### Cliente
- `Intersect.Client.Core/General/Weather.cs` - Recepção de clima global
- `Intersect.Client.Core/Maps/MapInstance.cs` - Renderização de clima
- `Intersect.Client.Core/Networking/PacketHandler.cs` - Processamento de pacotes

### Framework
- `Framework/Intersect.Framework.Core/Network/Packets/Server/GlobalWeatherPacket.cs` - Pacote de rede

## 📝 Exemplos de Uso

### Cenário 1: Chuva durante a noite
```csharp
// Em um evento de servidor que roda a cada hora
var hour = Time.GetTime().Hour;
if (hour >= 20 || hour < 6) // 20:00 às 06:00
{
    Weather.SetWeather(rainAnimId, 2, 3, 60);
}
else
{
    Weather.SetWeather(Guid.Empty, 0, 0, 0);
}
PacketSender.SendGlobalWeatherToAll();
```

### Cenário 2: Sistema de Estações
```csharp
// Baseado em uma variável de servidor "CurrentSeason"
switch (currentSeason)
{
    case "Spring":
        Weather.SetWeather(rainAnimId, 2, 3, 40);
        break;
    case "Summer":
        Weather.SetWeather(Guid.Empty, 0, 0, 0);
        break;
    case "Autumn":
        Weather.SetWeather(rainAnimId, 3, 4, 50);
        break;
    case "Winter":
        Weather.SetWeather(snowAnimId, 1, 2, 70);
        break;
}
PacketSender.SendGlobalWeatherToAll();
```

### Cenário 3: Evento de Tempestade
```csharp
// Quando um boss especial spawna
public void OnBossSpawn()
{
    Weather.SetWeather(stormAnimId, 5, 5, 100);
    PacketSender.SendGlobalWeatherToAll();
}

public void OnBossDeath()
{
    Weather.SetWeather(Guid.Empty, 0, 0, 0);
    PacketSender.SendGlobalWeatherToAll();
}
```

## ⚙️ Configurações Recomendadas

### Chuva Leve
- XSpeed: 1-2
- YSpeed: 2-3
- Intensity: 30-40

### Chuva Moderada
- XSpeed: 2-3
- YSpeed: 3-4
- Intensity: 50-60

### Chuva Forte/Tempestade
- XSpeed: 4-5
- YSpeed: 5-6
- Intensity: 80-100

### Neve Leve
- XSpeed: 0-1
- YSpeed: 1-2
- Intensity: 30-50

### Neve Pesada
- XSpeed: 1-2
- YSpeed: 2-3
- Intensity: 70-90

## 🐛 Troubleshooting

### O clima não aparece
1. Verifique se o mapa está marcado como outdoor (`IsIndoors = false`)
2. Confirme que a animação existe no banco de dados
3. Verifique se o servidor está rodando `Weather.Update()` no loop principal

### Clima diferente entre jogadores
1. Verifique se `PacketSender.SendGlobalWeatherToAll()` está sendo chamado
2. Confirme que não há erros no console do servidor

### Performance ruim
1. Reduza a intensidade do clima
2. Use animações otimizadas com menos partículas
3. Considere desativar clima em áreas com muitos jogadores

## 📚 Referências

- ID da sua animação de chuva: `dcd0472c-264b-4e8f-9250-065fd54460c2`
- Comando de teste: `weather dcd0472c-264b-4e8f-9250-065fd54460c2 2 3 50`
- Comando para limpar: `weather clear`

## 🎯 Próximos Passos

1. **Crie mais animações de clima** (neve, neblina, areia, etc.)
2. **Implemente sistema dinâmico** baseado em horário ou eventos
3. **Adicione comandos no jogo** para moderadores controlarem o clima
4. **Crie eventos especiais** com climas únicos

---

**Desenvolvido para Rebornia Engine**  
Sistema implementado com sucesso! ✅
