using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Maps;
using Intersect.Client.General;
using Intersect.Client.Core;
using Intersect.Framework.Core;
using Intersect.Utilities;

namespace Intersect.Client.Maps;

public partial class ActionMessage : IActionMessage
{
    private const float ANIMATION_DURATION = 1000f;
    private const float PEAK_HEIGHT = 60f;
    private const float HORIZONTAL_DRIFT = 30f;
    private const int DIGIT_SPACING = 2;
    private const int DIGIT_WIDTH = 16;
    private const int DIGIT_HEIGHT = 24;
    private const float CRITICAL_SCALE = 0.6f;
    private const float STATUS_SCALE = 1.0f;
    private const float MAX_CRITICAL_ROTATION = 15f;
    private const float MAX_STATUS_ROTATION = 10f;

    // Flag global para marcar se a próxima mensagem de dano deve ser crítica
    private static bool _nextDamageIsCritical = false;
    private static long _criticalFlagTime = 0;
    
    // Flags globais para status icons (para quando vier seguido de número)
    private static string _nextStatusIcon = string.Empty;
    private static long _statusFlagTime = 0;
    
    private const long FLAG_TIMEOUT = 100; // 100ms de timeout

    // Mapeamento de palavras-chave para nomes de texturas (sem o .png)
    private static readonly Dictionary<string, string> StatusTextureMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "SILENCED", "silenced" },
        { "STUNNED", "stunned" },
        { "SHIELD", "shield" },
        { "BLINDED", "blinded" },
        { "SNARED", "snared" },
        { "SLEEP", "sleep" },
        { "STEALTH", "stealth" },
        { "INVULNERABLE", "invulnerable" },
        { "CLEANSED", "cleansed" },
        { "TRANSFORMED", "transformed" },
        { "TAUNT", "taunt" }
    };

    public Color Color { get; init; }
    public IMapInstance Map { get; init; }
    public string Text { get; init; }
    public long TransmissionTimer { get; init; }
    public long StartTime { get; init; }
    public int X { get; init; }
    public int XOffset { get; init; }
    public int Y { get; init; }

    private List<IGameTexture> _digitTextures = new();
    private IGameTexture? _criticalTexture = null;
    private IGameTexture? _statusTexture = null;
    private bool _texturesLoaded = false;
    private bool _isCritical = false;
    private bool _hasStatus = false;
    private float _criticalRotation = 0f;
    private float _statusRotation = 0f;

    public ActionMessage(MapInstance map, int x, int y, string text, Color color)
    {
        Map = map;
        X = x;
        Y = y;
        Text = text;
        Color = color;
        XOffset = Globals.Random.Next(-30, 30);
        StartTime = Timing.Global.MillisecondsUtc;
        TransmissionTimer = StartTime + (long)ANIMATION_DURATION;
        
        var hasNumbers = text.Any(char.IsDigit);
        
        // Sistema de flags para CRITICAL (exatamente como está)
        var hasCriticalText = text.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase) || 
                               text.Contains("CRIT", StringComparison.OrdinalIgnoreCase);
        
        if (hasCriticalText && !hasNumbers)
        {
            _nextDamageIsCritical = true;
            _criticalFlagTime = Timing.Global.MillisecondsUtc;
            return;
        }
        
        if (hasNumbers && _nextDamageIsCritical && 
            (Timing.Global.MillisecondsUtc - _criticalFlagTime) < FLAG_TIMEOUT)
        {
            _isCritical = true;
            _nextDamageIsCritical = false;
            _criticalRotation = (float)(Globals.Random.NextDouble() * MAX_CRITICAL_ROTATION * 2 - MAX_CRITICAL_ROTATION);
            Audio.AddGameSound("critical", false);
        }
        else
        {
            if ((Timing.Global.MillisecondsUtc - _criticalFlagTime) >= FLAG_TIMEOUT)
            {
                _nextDamageIsCritical = false;
            }
        }
        
        // Sistema de STATUS ICONS - MODIFICADO para suportar com e sem números
        if (!hasNumbers)
        {
            // Esta mensagem NÃO tem números, verificar se é uma mensagem de status
            foreach (var statusEntry in StatusTextureMap)
            {
                if (text.Contains(statusEntry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // Carregar a textura do status AGORA e exibir apenas o ícone
                    _statusTexture = Globals.ContentManager.GetTexture(TextureType.Misc, $"{statusEntry.Value}.png");
                    if (_statusTexture != null)
                    {
                        _hasStatus = true;
                        _statusRotation = (float)(Globals.Random.NextDouble() * MAX_STATUS_ROTATION * 2 - MAX_STATUS_ROTATION);
                        _texturesLoaded = true; // Marcar como carregado mesmo sem números
                        
                        // TAMBÉM marcar a flag para a próxima mensagem COM números (se houver)
                        _nextStatusIcon = statusEntry.Value;
                        _statusFlagTime = Timing.Global.MillisecondsUtc;
                    }
                    return; // Exibir apenas o ícone de status
                }
            }
        }
        
        // Se esta mensagem TEM números, verificar se deve usar o status icon (sistema de flag)
        if (hasNumbers && !string.IsNullOrEmpty(_nextStatusIcon) && 
            (Timing.Global.MillisecondsUtc - _statusFlagTime) < FLAG_TIMEOUT)
        {
            _hasStatus = true;
            _statusRotation = (float)(Globals.Random.NextDouble() * MAX_STATUS_ROTATION * 2 - MAX_STATUS_ROTATION);
            
            // Carregar a textura do status
            var statusIconName = _nextStatusIcon;
            _nextStatusIcon = string.Empty; // Limpar a flag após usar
            
            _statusTexture = Globals.ContentManager.GetTexture(TextureType.Misc, $"{statusIconName}.png");
        }
        else
        {
            if ((Timing.Global.MillisecondsUtc - _statusFlagTime) >= FLAG_TIMEOUT)
            {
                _nextStatusIcon = string.Empty;
            }
        }
        
        LoadDigitTextures();
    }

    private void LoadDigitTextures()
    {
        _digitTextures.Clear();

        var numbers = new string(Text.Where(char.IsDigit).ToArray());
        
        foreach (var digit in numbers)
        {
            var textureName = $"{digit}.png";
            var texture = Globals.ContentManager.GetTexture(TextureType.Misc, textureName);
            if (texture != null)
            {
                _digitTextures.Add(texture);
            }
        }

        if (_isCritical)
        {
            _criticalTexture = Globals.ContentManager.GetTexture(TextureType.Misc, "critical.png");
        }

        // Marcar como carregado se há dígitos OU se há status (permite exibir apenas o ícone)
        _texturesLoaded = _digitTextures.Count > 0 || _hasStatus;
    }

    private float CalculateYOffset(float progress)
    {
        return -4 * PEAK_HEIGHT * progress * (progress - 1);
    }

    private float CalculateXOffset(float progress)
    {
        return -HORIZONTAL_DRIFT * progress;
    }

    private byte CalculateAlpha(float progress)
    {
        if (progress > 0.8f)
        {
            var fadeProgress = (progress - 0.8f) / 0.2f;
            return (byte)(255 * (1 - fadeProgress));
        }
        return 255;
    }

    public void Draw(int mapX, int mapY, int tileWidth, int tileHeight)
    {
        if (!_texturesLoaded)
        {
            return;
        }

        var elapsed = Timing.Global.MillisecondsUtc - StartTime;
        var progress = Math.Min(1.0f, elapsed / ANIMATION_DURATION);

        var yOffset = CalculateYOffset(progress);
        var xOffset = CalculateXOffset(progress);
        var alpha = CalculateAlpha(progress);

        var baseX = (float)(mapX + X * tileWidth + XOffset);
        var baseY = (float)(mapY + Y * tileHeight - (tileHeight * 2));

        baseX += xOffset;
        baseY -= yOffset;

        // Cor com alpha para números e critical (usa a cor do dano)
        var renderColor = new Color(alpha, Color.R, Color.G, Color.B);
        
        // Cor branca com alpha para ícones de status (preserva cores originais da imagem)
        var whiteColor = new Color(alpha, 255, 255, 255);

        // Desenhar CRITICAL ou STATUS icon (CAMADA 1 - atrás dos números)
        if (_isCritical && _criticalTexture != null)
        {
            var criticalX = baseX - (_criticalTexture.Width / 2f);
            var criticalY = baseY - (_criticalTexture.Height / 2f);

            var oscillation = (float)(Math.Sin(progress * Math.PI * 2) * (MAX_CRITICAL_ROTATION * 0.5f));
            var rotation = _criticalRotation + oscillation;

            // CRITICAL usa a cor do dano (vermelho/verde/etc)
            Intersect.Client.Core.Graphics.DrawGameTexture(
                _criticalTexture,
                new FloatRect(0, 0, _criticalTexture.Width, _criticalTexture.Height),
                new FloatRect(criticalX, criticalY, _criticalTexture.Width, _criticalTexture.Height),
                renderColor, // USA COR DO DANO
                null,
                GameBlendModes.None,
                null,
                rotation
            );
        }
        else if (_hasStatus && _statusTexture != null)
        {
            var statusX = baseX - (_statusTexture.Width / 2f);
            var statusY = baseY - (_statusTexture.Height / 2f);

            var oscillation = (float)(Math.Sin(progress * Math.PI * 2) * (MAX_STATUS_ROTATION * 0.5f));
            var rotation = _statusRotation + oscillation;

            // STATUS usa cor branca (preserva cores originais da textura)
            Intersect.Client.Core.Graphics.DrawGameTexture(
                _statusTexture,
                new FloatRect(0, 0, _statusTexture.Width, _statusTexture.Height),
                new FloatRect(statusX, statusY, _statusTexture.Width, _statusTexture.Height),
                whiteColor, // USA COR BRANCA para preservar cores originais
                null,
                GameBlendModes.None,
                null,
                rotation
            );
        }

        // Desenhar números (CAMADA 2 - em cima do icon) - APENAS SE HOUVER NÚMEROS
        if (_digitTextures.Count > 0)
        {
            var digitScale = (_isCritical && _criticalTexture != null) ? CRITICAL_SCALE : STATUS_SCALE;
            
            var scaledDigitWidth = DIGIT_WIDTH * digitScale;
            var scaledDigitHeight = DIGIT_HEIGHT * digitScale;
            var scaledSpacing = DIGIT_SPACING * digitScale;
            
            var totalDigitsWidth = (_digitTextures.Count * scaledDigitWidth) + ((_digitTextures.Count - 1) * scaledSpacing);

            var currentX = baseX - (totalDigitsWidth / 2f);
            var currentY = baseY - (scaledDigitHeight / 2f);

            for (var i = 0; i < _digitTextures.Count; i++)
            {
                var texture = _digitTextures[i];

                // Números usam a cor do dano
                Intersect.Client.Core.Graphics.DrawGameTexture(
                    texture,
                    new FloatRect(0, 0, texture.Width, texture.Height),
                    new FloatRect(currentX, currentY, scaledDigitWidth, scaledDigitHeight),
                    renderColor // USA COR DO DANO para os números
                );

                currentX += scaledDigitWidth + scaledSpacing;
            }
        }
    }

    public void TryRemove()
    {
        if (TransmissionTimer <= Timing.Global.MillisecondsUtc)
        {
            (Map as MapInstance)?.ActionMessages.Remove(this);
            _digitTextures.Clear();
            _criticalTexture = null;
            _statusTexture = null;
            _texturesLoaded = false;
        }
    }
}
