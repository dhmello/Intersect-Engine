# Sistema de Autonomia de Movimentação dos NPCs

## Visão Geral

Este documento descreve as melhorias implementadas no sistema de movimentação dos NPCs para torná-los mais autônomos, suaves e com comportamentos individualizados.

## Problema Original

O sistema anterior tinha as seguintes limitações:

1. **Movimento Sincronizado**: Todos os NPCs se moviam ao mesmo tempo, criando um padrão artificial
2. **Trajetórias Curtas**: NPCs andavam apenas 1-3 tiles antes de mudar de direção
3. **Comportamento Previsível**: Padrões de movimento muito regulares e previsíveis
4. **Falta de Individualidade**: Todos os NPCs do mesmo tipo se comportavam identicamente

## Soluções Implementadas

### 1. Temporização Individual

Cada NPC agora possui seu próprio timing de movimento:

```csharp
// Variáveis adicionadas
private long _individualMovementDelay;  // Delay único para cada NPC (500-1500ms)
private long _nextMovementTime;         // Próximo momento de movimento
```

**Benefícios**:
- NPCs não se movem mais simultaneamente
- Cada NPC tem sua própria "velocidade" de decisão
- Variação de 500-1500ms entre NPCs cria movimento mais natural

### 2. Padrões de Movimento Extendidos

Sistema de trajetórias mais longas e variadas:

```csharp
private int _consecutiveMovesInDirection;  // Contador de movimentos na direção atual
private int _maxConsecutiveMoves;          // Máximo de movimentos em uma direção (3-12)
private Direction? _currentMovementPattern; // Direção do padrão atual
```

**Benefícios**:
- NPCs andam de 3 a 12 tiles na mesma direção
- Trajetórias mais longas e naturais
- Menos mudanças de direção aleatórias

### 3. Sistema de Pausas Aleatórias

NPCs ocasionalmente param de se mover:

```csharp
private long _nextPauseTime;   // Quando o próximo pause ocorrerá
private long _pauseDuration;   // Duração do pause
private bool _isPaused;        // Se está pausado atualmente
```

**Comportamento**:
- 15% de chance de pausar ao atingir o tempo de pausa
- Pausas duram entre 500-2500ms
- 50% de chance de girar aleatoriamente durante a pausa
- Novo intervalo de pausa: 5-20 segundos

### 4. Variação de Movimento Dinâmica

```csharp
// No movimento, aplica variação adicional
var randomVariation = Randomization.Next(-100, 300);
_nextMovementTime = currentTime + movementTime + _individualMovementDelay + randomVariation;
```

**Benefícios**:
- Timing nunca é exatamente o mesmo
- Movimento mais orgânico e natural
- Evita sincronização acidental entre NPCs

### 5. Mudanças de Direção Sutis

```csharp
// 10% de chance de mudar direção mid-pattern
if (Randomization.Next(0, 100) < 10)
{
    var directionShift = Randomization.Next(-1, 2); // Muda ±1 direção
    // ...aplica mudança sutil
}
```

**Benefícios**:
- Movimentos menos mecânicos
- Trajetórias levemente curvas
- Comportamento mais realista

## Inicialização

Cada NPC recebe características únicas ao ser criado:

```csharp
private void InitializeMovementAutonomy()
{
    // Delay individual (500-1500ms)
    _individualMovementDelay = Randomization.Next(500, 1500);
    
    // Tempo inicial randomizado (0-2000ms offset)
    _nextMovementTime = Timing.Global.Milliseconds + Randomization.Next(0, 2000);
    
    // Padrão de movimento (2-8 tiles de variação, 3-12 max consecutivos)
    _movementVariation = Randomization.Next(2, 8);
    _maxConsecutiveMoves = Randomization.Next(3, 12);
    
    // Sistema de pausas (primeira pausa em 5-15s)
    _nextPauseTime = Timing.Global.Milliseconds + Randomization.Next(5000, 15000);
}
```

## Tipos de Movimento Aprimorados

### StandStill
- Ainda para, mas com timing individual
- Evita que NPCs "travem" no mesmo frame

### TurnRandomly  
- Gira com intervalo personalizado
- Delay base + variação de 500-2500ms

### MoveRandomly
- Usa o novo sistema `MoveRandomlyEnhanced()`
- Todos os benefícios de autonomia aplicados

## Comportamento em Reset

Quando um NPC é resetado, ele:
1. Reinicializa todos os parâmetros de movimento
2. Recebe novos valores aleatórios
3. Evita padrões repetitivos após respawn

## Performance

As mudanças são otimizadas para não impactar performance:
- Apenas algumas variáveis adicionais por NPC
- Cálculos simples e rápidos
- Sem loops ou operações pesadas

## Resultado Final

### Antes:
- ❌ Todos os NPCs se movem juntos
- ❌ Trajetórias curtas (1-3 tiles)
- ❌ Padrões previsíveis
- ❌ Comportamento idêntico entre NPCs

### Depois:
- ✅ Cada NPC tem seu próprio timing
- ✅ Trajetórias longas e variadas (3-12 tiles)
- ✅ Pausas naturais ocasionais
- ✅ Mudanças de direção sutis
- ✅ Comportamento único para cada NPC
- ✅ Movimento muito mais natural e orgânico

## Exemplo de Comportamento

Imagine 5 NPCs em uma área:

**Antes**:
- Todos se movem ao mesmo tempo
- Todos andam 2 tiles e param
- Todos param no mesmo momento
- Padrão mecânico e artificial

**Depois**:
- NPC 1: Move-se, pausa 1s, move 5 tiles para norte, pausa 800ms, gira, continua
- NPC 2: Pausa 500ms, move 8 tiles para leste com leve curva, pausa 2s
- NPC 3: Move 4 tiles para sul, muda para sudeste, continua 3 tiles, pausa
- NPC 4: Parado pausando por 1.5s, então se move
- NPC 5: Movendo 12 tiles em linha reta para oeste

Cada NPC tem sua própria "personalidade" de movimento!

## Compatibilidade

✅ Totalmente compatível com:
- Sistema de pathfinding existente
- Combate e targeting
- NPCs em fuga (fleeing)
- Sistemas de reset e aggro
- Todas as configurações de NPC existentes

## Testes Recomendados

1. Spawne múltiplos NPCs do mesmo tipo em uma área
2. Observe se eles se movem de forma independente
3. Verifique se as trajetórias são mais longas
4. Confirme que há pausas ocasionais
5. Teste em combate para garantir que não afeta perseguição

## Notas Técnicas

- Sistema usa `Randomization` existente da engine
- Mantém compatibilidade com `MoveTimer` global
- Não altera protocolos de rede
- Mudanças apenas server-side, clientes veem o resultado
