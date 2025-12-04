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
    private const float ANIMATION_DURATION = 1000f; // Duração total da animação em ms
    private const float PEAK_HEIGHT = 60f; // Altura máxima do arco (em pixels)
    private const float HORIZONTAL_DRIFT = 30f; // Deslocamento horizontal para criar o efeito de U
    private const int DIGIT_SPACING = 2; // Espaçamento entre dígitos
    private const int DIGIT_WIDTH = 16; // Largura esperada de cada dígito
    private const int DIGIT_HEIGHT = 24; // Altura esperada de cada dígito
    private const float CRITICAL_SCALE = 0.6f; // Escala dos números quando é crítico (60% do tamanho original)
    private const float MAX_CRITICAL_ROTATION = 15f; // Rotação máxima do balão crítico em graus

    // Flag global para marcar se a próxima mensagem de dano deve ser crítica
    private static bool _nextDamageIsCritical = false;
    private static long _criticalFlagTime = 0;
    private const long CRITICAL_FLAG_TIMEOUT = 100; // 100ms de timeout para o flag de crítico

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
    private bool _texturesLoaded = false;
    private bool _isCritical = false;
    private float _criticalRotation = 0f; // Rotação aleatória do balão crítico

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
        
        // Verificar se esta mensagem É a mensagem de "CRITICAL" (não contém números)
        var hasNumbers = text.Any(char.IsDigit);
        var hasCriticalText = text.Contains("CRITICAL", StringComparison.OrdinalIgnoreCase) || 
                               text.Contains("CRIT", StringComparison.OrdinalIgnoreCase);
        
        // Se a mensagem contém "CRITICAL" mas NÃO tem números, é a mensagem de aviso de crítico
        // Então marcamos que a PRÓXIMA mensagem de dano deve ser crítica
        if (hasCriticalText && !hasNumbers)
        {
            _nextDamageIsCritical = true;
            _criticalFlagTime = Timing.Global.MillisecondsUtc;
            // Esta mensagem não será renderizada (não tem números)
            return;
        }
        
        // Se esta mensagem tem números e o flag de crítico está ativo E não expirou
        if (hasNumbers && _nextDamageIsCritical && 
            (Timing.Global.MillisecondsUtc - _criticalFlagTime) < CRITICAL_FLAG_TIMEOUT)
        {
            _isCritical = true;
            _nextDamageIsCritical = false; // Resetar o flag
            
            // Gerar rotação aleatória
            _criticalRotation = (float)(Globals.Random.NextDouble() * MAX_CRITICAL_ROTATION * 2 - MAX_CRITICAL_ROTATION);
        }
        else
        {
            // Resetar flag se expirou
            if ((Timing.Global.MillisecondsUtc - _criticalFlagTime) >= CRITICAL_FLAG_TIMEOUT)
            {
                _nextDamageIsCritical = false;
            }
        }
        
        LoadDigitTextures();
    }

    private void LoadDigitTextures()
    {
        _digitTextures.Clear();

        // Extrair apenas números do texto
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

        // Carregar textura de crítico se for um dano crítico
        if (_isCritical)
        {
            _criticalTexture = Globals.ContentManager.GetTexture(TextureType.Misc, "critical.png");
        }

        _texturesLoaded = _digitTextures.Count > 0;
    }

    /// <summary>
    /// Calcula a posição Y com base na animação em U invertido
    /// </summary>
    /// <param name="progress">Progresso da animação de 0 a 1</param>
    /// <returns>Offset Y para a posição</returns>
    private float CalculateYOffset(float progress)
    {
        // Função parabólica para criar o efeito de U invertido
        // y = -4 * height * progress * (progress - 1)
        return -4 * PEAK_HEIGHT * progress * (progress - 1);
    }

    /// <summary>
    /// Calcula a posição X com base na animação (movimento para esquerda)
    /// </summary>
    /// <param name="progress">Progresso da animação de 0 a 1</param>
    /// <returns>Offset X para a posição</returns>
    private float CalculateXOffset(float progress)
    {
        // Movimento suave para a esquerda
        return -HORIZONTAL_DRIFT * progress;
    }

    /// <summary>
    /// Calcula o alfa baseado no progresso (fade out no final)
    /// </summary>
    /// <param name="progress">Progresso da animação de 0 a 1</param>
    /// <returns>Valor de alfa de 0 a 255</returns>
    private byte CalculateAlpha(float progress)
    {
        // Fade out nos últimos 20% da animação
        if (progress > 0.8f)
        {
            var fadeProgress = (progress - 0.8f) / 0.2f;
            return (byte)(255 * (1 - fadeProgress));
        }
        return 255;
    }

    /// <summary>
    /// Desenha as imagens dos dígitos com animação
    /// </summary>
    public void Draw(int mapX, int mapY, int tileWidth, int tileHeight)
    {
        if (!_texturesLoaded || _digitTextures.Count == 0)
        {
            return;
        }

        // Calcular progresso da animação (0 a 1)
        var elapsed = Timing.Global.MillisecondsUtc - StartTime;
        var progress = Math.Min(1.0f, elapsed / ANIMATION_DURATION);

        // Calcular offsets da animação
        var yOffset = CalculateYOffset(progress);
        var xOffset = CalculateXOffset(progress);
        var alpha = CalculateAlpha(progress);

        // Posição base centralizada (usar float para permitir cálculos precisos)
        var baseX = (float)(mapX + X * tileWidth + XOffset);
        var baseY = (float)(mapY + Y * tileHeight - (tileHeight * 2));

        // Aplicar offsets de animação à posição base
        baseX += xOffset;
        baseY -= yOffset;

        // Criar cor com alpha modificado
        var renderColor = new Color(alpha, Color.R, Color.G, Color.B);

        // Determinar a escala dos números (menor quando é crítico para caber no balão)
        var digitScale = _isCritical && _criticalTexture != null ? CRITICAL_SCALE : 1.0f;
        
        // Calcular largura e altura dos dígitos com escala aplicada
        var scaledDigitWidth = DIGIT_WIDTH * digitScale;
        var scaledDigitHeight = DIGIT_HEIGHT * digitScale;
        var scaledSpacing = DIGIT_SPACING * digitScale;
        
        // Calcular largura total dos dígitos escalados
        var totalDigitsWidth = (_digitTextures.Count * scaledDigitWidth) + ((_digitTextures.Count - 1) * scaledSpacing);

        // Desenhar imagem de crítico atrás dos números se aplicável (CAMADA 1)
        if (_isCritical && _criticalTexture != null)
        {
            // Centralizar a imagem critical.png
            var criticalX = baseX - (_criticalTexture.Width / 2f);
            var criticalY = baseY - (_criticalTexture.Height / 2f);

            // Desenhar com rotação aleatória
            Intersect.Client.Core.Graphics.DrawGameTexture(
                _criticalTexture,
                new FloatRect(0, 0, _criticalTexture.Width, _criticalTexture.Height),
                new FloatRect(criticalX, criticalY, _criticalTexture.Width, _criticalTexture.Height),
                renderColor, // Aplicar a mesma cor do tipo de dano
                null, // renderTarget
                GameBlendModes.None, // blendMode
                null, // shader
                _criticalRotation // rotação em graus
            );
        }

        // Desenhar cada dígito CENTRALIZADO e ESCALADO em cima da imagem critical (CAMADA 2)
        var currentX = baseX - (totalDigitsWidth / 2f);
        var currentY = baseY - (scaledDigitHeight / 2f); // Centralizar verticalmente os números escalados

        for (var i = 0; i < _digitTextures.Count; i++)
        {
            var texture = _digitTextures[i];

            // Desenhar o dígito com escala aplicada
            Intersect.Client.Core.Graphics.DrawGameTexture(
                texture,
                new FloatRect(0, 0, texture.Width, texture.Height),
                new FloatRect(currentX, currentY, scaledDigitWidth, scaledDigitHeight), // Aplicar escala
                renderColor // Aplicar a mesma cor do tipo de dano
            );

            // Avançar para o próximo dígito
            currentX += scaledDigitWidth + scaledSpacing;
        }
    }

    public void TryRemove()
    {
        if (TransmissionTimer <= Timing.Global.MillisecondsUtc)
        {
            (Map as MapInstance)?.ActionMessages.Remove(this);
            _digitTextures.Clear();
            _criticalTexture = null;
            _texturesLoaded = false;
        }
    }
}
