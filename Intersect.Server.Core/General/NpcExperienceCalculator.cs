using Intersect.Framework.Core.GameObjects.NPCs;

namespace Intersect.Server.General;

/// <summary>
/// Calcula a experiência que um NPC deve dar baseado em seu nível.
/// Usa a mesma fórmula que você usava no editor, mas agora é calculado automaticamente pelo servidor.
/// </summary>
public static partial class NpcExperienceCalculator
{
    /// <summary>
    /// Fator de ajuste da experiência. Valores menores = mais XP, valores maiores = menos XP.
    /// Valor padrão: 0.5
    /// </summary>
    private const double ExperienceFactor = 0.5;

    /// <summary>
    /// Define se o sistema deve SEMPRE usar o cálculo automático, ignorando valores do banco de dados.
    /// true = sempre calcula automaticamente
    /// false = usa valor do banco se configurado manualmente
    /// </summary>
    private const bool AlwaysUseAutomaticCalculation = true;

    /// <summary>
    /// Multiplicador de XP para NPCs marcados como [BOSS]
    /// </summary>
    private const int BossExperienceMultiplier = 30;

    /// <summary>
    /// Tag que identifica um NPC como Boss
    /// </summary>
    private const string BossTag = "[BOSS]";

    /// <summary>
    /// Calcula a experiência que um NPC deve dar baseado em seu nível.
    /// Fórmula: baseexp / (2.4 * level * fator)
    /// Onde baseexp = (50/3) * (level³ - 6*level² + 17*level - 12)
    /// </summary>
    /// <param name="level">Nível do NPC</param>
    /// <returns>Experiência calculada</returns>
    public static long CalculateExperience(int level)
    {
        if (level <= 0)
        {
            return 0;
        }

        // Cálculo da experiência base
        var levelCubed = Math.Pow(level, 3);
        var levelSquared = Math.Pow(level, 2);
        var baseExp = (50.0 / 3.0) * (levelCubed - 6 * levelSquared + 17 * level - 12);

        // Cálculo da XP final
        var experience = baseExp / (2.4 * level * ExperienceFactor);

        // Garantir que a XP seja pelo menos 1
        return (long)Math.Max(1, Math.Round(experience));
    }

    /// <summary>
    /// Verifica se o NPC é um Boss baseado no nome
    /// </summary>
    /// <param name="npcName">Nome do NPC</param>
    /// <returns>True se o NPC tem a tag [BOSS] no nome</returns>
    private static bool IsBoss(string npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName))
        {
            return false;
        }

        return npcName.Contains(BossTag, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calcula a experiência usando a fórmula automática baseada no nível do NPC.
    /// IMPORTANTE: Se AlwaysUseAutomaticCalculation = true, SEMPRE usa o cálculo automático, 
    /// ignorando o valor configurado no editor.
    /// NPCs com [BOSS] no nome recebem multiplicador de XP x30.
    /// </summary>
    /// <param name="npcDescriptor">Descritor do NPC</param>
    /// <returns>Experiência que o NPC deve dar</returns>
    public static long GetNpcExperience(NPCDescriptor npcDescriptor)
    {
        if (npcDescriptor == null)
        {
            return 0;
        }

        long experience;

        // Se configurado para SEMPRE usar cálculo automático
        if (AlwaysUseAutomaticCalculation)
        {
            experience = CalculateExperience(npcDescriptor.Level);
        }
        // Se a XP foi configurada manualmente (> 0), usar o valor manual
        else if (npcDescriptor.Experience > 0)
        {
            experience = npcDescriptor.Experience;
        }
        // Caso contrário, calcular automaticamente baseado no nível
        else
        {
            experience = CalculateExperience(npcDescriptor.Level);
        }

        // Aplicar multiplicador de Boss se o NPC tiver [BOSS] no nome
        if (IsBoss(npcDescriptor.Name))
        {
            experience *= BossExperienceMultiplier;
        }

        return experience;
    }
}
