/*
 * This code is adapted from the Advance Admin ESP plugin for CSGO by MitchDizzle
 * https://github.com/MitchDizzle/Advanced-Admin-ESP/blob/master/scripting/csgo_advanced_esp.sp
 *
 */


using System.Drawing;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;


namespace ESP;

public class EspPlugin : BasePlugin
{

    public override string ModuleName => "ESP";

    public override string ModuleVersion => "0.0.1";

    private bool[]? IsUsingEsp { get; set; }
    private bool[]? CanSeeEsp { get; set; }
    private Dictionary<int, CBaseModelEntity> PlayerModels { get; set; }
  

    public override void Load(bool hotReload)
    {
        IsUsingEsp = Enumerable.Repeat(false, 16).ToArray();
        CanSeeEsp = Enumerable.Repeat(false, 16).ToArray();
        PlayerModels = new Dictionary<int, CBaseModelEntity>();
        CheckGlows();
        Console.WriteLine("Hello World!");
        RegisterListener<Listeners.OnServerPrecacheResources> (manifest =>
            manifest.AddResource("characters/models/ctm_sas/ctm_sas.vmdl")
            );
        RegisterListener<Listeners.OnMapStart>(map =>
        {
            Server.PrecacheModel("characters/models/ctm_sas/ctm_sas.vmdl");
            
        });
        
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerConnect> (OnClientConnect);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventCsMatchEndRestart>(OnMapStart);
        
        
        
        RegisterListener<Listeners.CheckTransmit>(infoList =>
        {
            foreach (KeyValuePair<int, CBaseModelEntity> entity in PlayerModels)
            {
                foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
                {
                    if (player != null && IsUsingEsp[player.Slot])
                    {
                        info.TransmitEntities.Add((int) entity.Value.Index);
                    }
                }
            }
        });
    }

    private CCSPlayerController GetPlayerFromUserName(String playerName)
    {

        List<CCSPlayerController> players = Utilities.GetPlayers();

        foreach (CCSPlayerController player in players)
        {
            Console.WriteLine(player.PlayerName);
            Console.WriteLine(player.Slot);
            Console.WriteLine(player.SteamID);
            if (string.Equals(player.PlayerName, playerName, StringComparison.CurrentCultureIgnoreCase))
            {
                return player;
            }
        }

        throw new Exception($"Player {playerName} not found");
        
    }
    
    
    [ConsoleCommand("activate_esp", "This will turn on ESP for a player")]
    [CommandHelper(2, "[name],[bool]")]
    public void OnEspCommand(CCSPlayerController? player, CommandInfo command)
    {
        String playerName = command.ArgByIndex(1);
        bool espOn = command.ArgByIndex(2).ToLower() == "true";
        CCSPlayerController targetPlayer;
        
        Console.WriteLine($@"
            Arg Count: {command.ArgCount}
            Arg String: {command.ArgString}
            Command String: {command.GetCommandString}
            ESP On: {espOn}
            Player Name: {playerName}");
        
        if (player == null)
        {
            try
            { 
                targetPlayer = GetPlayerFromUserName(playerName);
            } catch(Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine(espOn
                ? $"ESP enabled for player {targetPlayer.PlayerName}"
                : $"ESP disabled for player {targetPlayer.PlayerName}");

            return;
        }
        
        try
        { 
            targetPlayer = GetPlayerFromUserName(playerName);
        } catch(Exception e)
        {
            command.ReplyToCommand(e.Message);
            return;
        }

        command.ReplyToCommand(espOn
            ? $"ESP enabled for player {targetPlayer.PlayerName}"
            : $"ESP disabled for player {targetPlayer.PlayerName}");
        if (IsUsingEsp != null) IsUsingEsp[targetPlayer.Slot] = espOn;
        CheckGlows();
    }
    
    [GameEventHandler]
    public HookResult OnMapStart(EventCsMatchEndRestart @event, GameEventInfo info)
    {
        ResetPlayerVars(-1);
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid?.Slot != null) ResetPlayerVars(@event.Userid.Slot);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnClientConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        if (@event.Userid?.Slot != null) ResetPlayerVars(@event.Userid.Slot);
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CheckGlows();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CheckGlows();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        DestroyGlows();
        return HookResult.Continue;
    }
    
    

    private void ResetPlayerVars(int client)
    {
        if (client == -1)
        {
            for (int i = 0; i <= Server.MaxPlayers; i++)
            {
                ResetPlayerVars(i);
            }
            return;
        }

        if (IsUsingEsp != null) IsUsingEsp[client] = false;
    }

    private void CheckGlows()
    {
        DestroyGlows();
        CreateGlows();

    }

    private void DestroyGlows()
    {
        for (int client = 1; client <= Server.MaxPlayers; client++)
        {
            CCSPlayerController? player = Utilities.GetPlayerFromUserid(client);
            if (player != null && player.Connected == PlayerConnectedState.PlayerConnected)
            {
                RemoveSkin(player);
            }
        }
    }

    private void CreateGlows()
    {
        for (int client = 1; client <= Server.MaxPlayers; client++)
        {
            CCSPlayerController? player = Utilities.GetPlayerFromUserid(client);
            if (player == null || !player.PawnIsAlive)
            {
                continue;
            }

            CBaseModelEntity? entity = CreatePlayerModelProp(player, true);
            if (entity == null)
            {
                continue;
            }
            SetGlowTeam(entity , player);

        }
    }

    private void SetGlowTeam(CBaseModelEntity? entity, CCSPlayerController? player)
    {
        if (player != null && player.Team != CsTeam.None)
        {
            SetupGlow(entity);
        }
    }

    private static void SetupGlow(CBaseModelEntity? entity)
    {
        
    }

    private CBaseModelEntity? CreatePlayerModelProp(CCSPlayerController player, bool bonemerge)
    {
        RemoveSkin(player);
        CDynamicProp? entity = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (player.IsValid != true || player.Connected != PlayerConnectedState.PlayerConnected)
        {
            return null;
        }

        CCSPlayerPawn? playerPawn = player.PlayerPawn.Value;

        if (playerPawn == null) return null;

        CBodyComponent? component = playerPawn.CBodyComponent;
        
        if (component == null) return null;
        
        CGameSceneNode? sceneNode = component.SceneNode;
        
        if (sceneNode == null) return null;
        
        CSkeletonInstance? skeletonInstance = sceneNode.GetSkeletonInstance();
        

        CModelState? modelState = skeletonInstance.ModelState;

        string? modelName = modelState.ModelName;

        if (entity != null)
        {
            Console.WriteLine(modelName);
            entity!.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked ((uint)~(1 << 0));
            entity.SetModel("");
            entity.Teleport(playerPawn.AbsOrigin, playerPawn.AbsRotation, null);
            entity.Spawnflags = 256;
            entity.DispatchSpawn();
            entity.RenderMode = RenderMode_t.kRenderGlow;
            entity.Collision.CollisionGroup = 0;
            entity.Glow.GlowColorOverride = Color.Blue;
            entity.Glow.GlowRange = 5000;
            entity.Glow.GlowRangeMin = 0;
            entity.Glow.GlowTeam = -1;
            entity.Glow.GlowType = 3;
            if (bonemerge)
            {
                
            }
            entity.AcceptInput("FollowEntity", player.PlayerPawn.Value, entity, "!activator");
            


            PlayerModels.TryAdd(player.Slot, entity);

            return entity;
        }
        return null;
    }

    private void RemoveSkin(CCSPlayerController? player)
    {
        if (player != null && PlayerModels.ContainsKey(player.Slot) && PlayerModels[player.Slot].IsValid)
        {
            PlayerModels[player.Slot].AcceptInput("Kill");
            PlayerModels.Remove(player.Slot);
        }
    }
    
}