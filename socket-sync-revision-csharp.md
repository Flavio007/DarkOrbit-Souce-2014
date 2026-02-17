# Socket Sync Revision (C# Server)

Este patch fecha a sincronizacao entre site (PHP) e servidor do jogo (C#) usando `SyncRevision`.

## O que ja foi feito no site

- A API agora envia `sync_revision` nas mutacoes de equipamento/loja.
- O backend PHP calcula e valida revisao de estado.
- Quando envia comando via socket para o C#, inclui `Parameters.SyncRevision` (quando aplicavel).

## O que aplicar no C# (SocketServer.cs)

### 1. Ler `SyncRevision` do payload

No `Execute(...)`, antes do `switch`, leia:

```csharp
var syncRevision = String(parameters?["SyncRevision"]);
```

### 2. Validar revisao antes de aplicar mutacao

Para acoes mutaveis (`BuyItem`, `UpdateStatus`, `RepairDrone`, `SellDrone`), validar:

```csharp
if (!IsSyncRevisionValid(player, syncRevision))
{
    // Estado do player no game esta atrasado vs site.
    // Opcao segura: recarregar status do banco e encerrar acao.
    ForceReloadFromDatabase(player);
    return;
}
```

### 3. Helper de validacao

```csharp
private static bool IsSyncRevisionValid(Player player, string expected)
{
    if (player?.GameSession == null) return false;
    if (string.IsNullOrWhiteSpace(expected)) return true; // backward compatibility

    var current = BuildPlayerSyncRevision(player);
    return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
}
```

### 4. Gerar revisao no C# igual ao backend

Use o mesmo principio: hash do estado relevante (saldo/data + inventario + loadout + drones).
Nao precisa copiar SQL do PHP, mas os campos precisam ser equivalentes.

```csharp
private static string BuildPlayerSyncRevision(Player player)
{
    // Exemplo: concatene snapshots estaveis do estado atual do player
    // e gere SHA-256 em UTF8.
    var seed = string.Join("|", new[]
    {
        player.Id.ToString(),
        player.Data?.credits.ToString() ?? "0",
        player.Data?.uridium.ToString() ?? "0",
        player.Ship?.Id.ToString() ?? "0",
        player.DroneManager?.Drones?.Count.ToString() ?? "0",
        // incluir loadout e inventario em formato deterministico
    });

    using (var sha = System.Security.Cryptography.SHA256.Create())
    {
        var bytes = Encoding.UTF8.GetBytes(seed);
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
```

### 5. Reconciliacao quando divergir

Implementar `ForceReloadFromDatabase(player)` para:

- recarregar `player.Data` do banco;
- recarregar inventario/equipment;
- chamar `player.DroneManager.UpdateDrones(true)` e `player.UpdateStatus()`.

Isso evita estado fantasma no cliente quando o game server recebeu comando atrasado.

## Compatibilidade

- Se `SyncRevision` nao vier no payload, mantenha fluxo atual (`return true`) para nao quebrar clientes antigos.
- Logue divergencias para monitorar frequencia:

```csharp
Logger.Log("sync_log", $"Sync mismatch user={player?.Id} action={action}");
```

