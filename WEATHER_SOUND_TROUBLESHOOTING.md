# 🌦️ Sistema de Clima Automático - Guia de Troubleshooting

## ✅ Checklist de Verificação

### 1. **Arquivos de Som**
Os arquivos de som devem estar na pasta `resources/sounds/` do **CLIENTE**:
- ✓ `chuva.wav`
- ✓ `tempestade.wav`
- ✓ `neve.wav`

**Importante:** Os arquivos devem ser `.wav` ou outro formato suportado pelo engine.

### 2. **Configuração no Servidor**
Verifique o arquivo `resources/config.json` do servidor:

```json
"Weather": {
  "EnableAutomaticWeather": true,
  "MinTimeBetweenChanges": 10,
  "MaxTimeBetweenChanges": 30,
  "ClearWeatherChance": 40,
  "WeatherTypes": [
    {
      "Id": "rain",
      "Name": "Chuva",
      "AnimationId": "dcd0472c-264b-4e8f-9250-065fd54460c2",
      "XSpeed": 2,
      "YSpeed": 3,
      "Intensity": 50,
      "Sound": "chuva",
      "SoundVolume": 0.3,
      "MinDuration": 5,
      "MaxDuration": 15,
      "Chance": 30,
      "CanOccurDay": true,
      "CanOccurNight": true,
      "Seasons": []
    }
  ]
}
```

### 3. **Testando Manualmente**
No console do servidor, use:
```
weather dcd0472c-264b-4e8f-9250-065fd54460c2
```

Você deve ver:
```
    Global weather set!
    Animation ID: dcd0472c-264b-4e8f-9250-065fd54460c2
    X Speed: 2
    Y Speed: 3
    Intensity: 50%
    Sound: chuva (Volume: 30%)
```

### 4. **Verificar se o Som Está Chegando ao Cliente**

Adicione logs temporários no `Weather.cs` do cliente:

```csharp
public static void LoadWeather(Guid animationId, int xSpeed, int ySpeed, int intensity, string sound, float soundVolume)
{
    Console.WriteLine($"[WEATHER DEBUG] Loading weather: Sound='{sound}', Volume={soundVolume}, Intensity={intensity}");
    
    // Stop previous weather sound if it changed
    if (_sound != sound && _currentWeatherSound != null)
    {
        Console.WriteLine($"[WEATHER DEBUG] Stopping previous sound: '{_sound}'");
        Audio.StopSound(_currentWeatherSound as IMapSound);
        _currentWeatherSound = null;
    }

    _animationId = animationId;
    _xSpeed = xSpeed;
    _ySpeed = ySpeed;
    _intensity = intensity;
    _sound = sound;
    _soundVolume = soundVolume;

    // Start new weather sound if applicable
    if (!string.IsNullOrEmpty(sound) && intensity > 0)
    {
        Console.WriteLine($"[WEATHER DEBUG] Attempting to play sound: '{sound}'");
        _currentWeatherSound = Audio.AddGameSound(sound, true);
        
        if (_currentWeatherSound != null)
        {
            Console.WriteLine($"[WEATHER DEBUG] Sound started successfully!");
        }
        else
        {
            Console.WriteLine($"[WEATHER DEBUG] ERROR: Failed to start sound!");
        }
    }
}
```

## 🔍 Problemas Comuns

### Som não toca
**Possíveis causas:**
1. ❌ Arquivo de som não existe na pasta `resources/sounds/`
2. ❌ Nome do arquivo incorreto (case-sensitive em alguns sistemas)
3. ❌ Volume do jogo está em 0%
4. ❌ Formato de arquivo não suportado
5. ❌ O campo `Sound` na configuração está vazio

**Solução:**
```bash
# Verifique se o arquivo existe:
dir "D:\Seu Jogo\resources\sounds\chuva.wav"

# Teste com um som existente:
weather dcd0472c-264b-4e8f-9250-065fd54460c2
```

### Clima não muda automaticamente
**Possíveis causas:**
1. ❌ `EnableAutomaticWeather` está `false`
2. ❌ Todos os climas têm `Chance: 0`
3. ❌ `ClearWeatherChance` está em 100%

**Solução:**
Verifique o `config.json` e reinicie o servidor.

### Animação aparece mas sem som
**Possíveis causas:**
1. ❌ Campo `Sound` vazio ou incorreto
2. ❌ `SoundVolume` está em 0.0

**Solução:**
```json
{
  "Sound": "chuva",
  "SoundVolume": 0.5
}
```

## 🎮 Comandos Úteis

### Forçar clima específico
```
weather dcd0472c-264b-4e8f-9250-065fd54460c2 2 3 50
```

### Limpar clima
```
weather clear
```

### Ver ajuda
```
weather -h
```

## 📝 Log de Debug do Servidor

O servidor deve mostrar logs como:
```
[Weather] Weather changed to Chuva for 8 minutes
```

Se não aparecer, verifique:
1. `EnableAutomaticWeather` está `true`?
2. O servidor foi reiniciado após alterar o config?
3. Há algum erro no console?

## 🔧 Testando o Sistema

### Teste 1: Som Manual
```csharp
// No console do servidor:
weather dcd0472c-264b-4e8f-9250-065fd54460c2
```
**Esperado:** Som deve tocar imediatamente no cliente.

### Teste 2: Som Automático
1. Configure `MinTimeBetweenChanges: 1` para testar rápido
2. Reinicie o servidor
3. Aguarde 1 minuto
4. Verifique se o clima mudou automaticamente

### Teste 3: Verificar Volume
1. No jogo, vá em Opções → Som
2. Certifique-se que "Volume de Efeitos" não está em 0%
3. Teste outro som do jogo (ex: atacar) para confirmar que o áudio funciona

## 📂 Estrutura de Arquivos

```
Seu Jogo/
├── Client/
│   └── resources/
│       └── sounds/
│           ├── chuva.wav          ← Arquivos aqui
│           ├── tempestade.wav
│           └── neve.wav
└── Server/
    └── resources/
        └── config.json             ← Configuração aqui
```

## 🆘 Ainda não funciona?

1. **Verifique os logs do console** (cliente e servidor)
2. **Teste com um som existente** no jogo
3. **Confirme que outros sons funcionam** no jogo
4. **Verifique se o formato do arquivo** é suportado (`.wav`, `.ogg`, `.mp3`)
5. **Reinicie** cliente e servidor
6. **Delete o cache** do cliente (se houver)

## 📧 Informações para Suporte

Se precisar de ajuda, forneça:
- ✓ Versão do engine
- ✓ Sistema operacional
- ✓ Log do console (cliente e servidor)
- ✓ Conteúdo do `config.json` (seção Weather)
- ✓ Lista de arquivos em `resources/sounds/`
- ✓ Outros sons funcionam no jogo?
