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

    public Color Color { get; init; }

    public IMapInstance Map { get; init; }

    public string Text { get; init; }

    public long TransmissionTimer { get; init; }

    public long StartTime { get; init; }

    public int X { get; init; }

    public int XOffset { get; init; }

    public int Y { get; init; }

    private List<IGameTexture> _digitTextures = new();
    private bool _texturesLoaded = false;

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

        // Calcular largura total dos dígitos
        var totalWidth = (_digitTextures.Count * DIGIT_WIDTH) + ((_digitTextures.Count - 1) * DIGIT_SPACING);

        // Posição base centralizada
        var baseX = mapX + X * tileWidth + XOffset;
        var baseY = mapY + Y * tileHeight - (tileHeight * 2);

        // Aplicar offsets de animação
        var currentX = baseX + xOffset - (totalWidth / 2f);
        var currentY = baseY - yOffset;

        // Desenhar cada dígito
        for (var i = 0; i < _digitTextures.Count; i++)
        {
            var texture = _digitTextures[i];
            
            // Criar cor com alpha modificado
            var renderColor = new Color(alpha, Color.R, Color.G, Color.B);

            // Desenhar o dígito
            Intersect.Client.Core.Graphics.DrawGameTexture(
                texture,
                new FloatRect(0, 0, texture.Width, texture.Height),
                new FloatRect(currentX, currentY, texture.Width, texture.Height),
                renderColor
            );

            // Avançar para o próximo dígito
            currentX += texture.Width + DIGIT_SPACING;
        }
    }

    public void TryRemove()
    {
        if (TransmissionTimer <= Timing.Global.MillisecondsUtc)
        {
            (Map as MapInstance)?.ActionMessages.Remove(this);
            _digitTextures.Clear();
            _texturesLoaded = false;
        }
    }
}
