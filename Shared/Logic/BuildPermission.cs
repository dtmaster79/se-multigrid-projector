using MultigridProjector.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.Entity;

namespace MultigridProjector.Logic
{
    // The checks the game's own MyProjectorBase.BuildInternal does since SE 1.211.018. MGP replaces
    // that method, so they are repeated here, otherwise MGP would let players around them:
    //
    // - a client builds only as itself: its player controls its own character, which is the
    //   builder, and the identity the block is built for (owner and builtBy) is the sender's
    // - the world limits (PCU, blocks per player, per grid and per type) for the identity the
    //   block is built for
    // - in survival without creative tools the builder (character or ship welder) carries at
    //   least one of the block's first component
    //
    // The DLC check stays in MultigridProjection.BuildInternal, as before.
    public static class BuildPermission
    {
        // The sender's player controls its own character, and that character is the builder
        [ServerOnly]
        public static bool IsSendersCharacter(ulong sender, long builder)
        {
            var player = MySession.Static.Players.GetPlayerById(new MyPlayer.PlayerId(sender));
            var character = player?.Character;
            return character != null
                && character.EntityId == builder
                && ReferenceEquals(player.Controller?.ControlledEntity, character);
        }

        // A vanilla BuildInternal RPC from a client. Vanilla clients and MGP clients in
        // client-only mode send owner = builtBy = their own identity. The game itself only
        // checks builtBy, MGP also checks owner, because MGP records owner as BuiltBy.
        [ServerOnly]
        public static bool AllowRpc(ulong sender, long owner, long builder, long builtBy, out string reason)
        {
            if (!IsSendersCharacter(sender, builder))
            {
                reason = "the builder is not the character the sender controls";
                return false;
            }

            var identityId = MySession.Static.Players.TryGetIdentityId(sender);
            if (identityId == 0 || builtBy != identityId || owner != identityId)
            {
                reason = "not the sender's own identity";
                return false;
            }

            reason = null;
            return true;
        }

        // World limits and building material, for every build request (RPC, full mode request,
        // local call from a ship welder). The block is built for the owner (BuiltBy = owner).
        [ServerOnly]
        public static bool AllowBuild(MySlimBlock previewBlock, MyCubeGrid builtGrid, long owner, long builder, bool requestInstant, ulong sender)
        {
            var session = MySession.Static;
            var definition = previewBlock.BlockDefinition;

            var instant = requestInstant && session.CreativeToolsEnabled(sender);
            var pcuToBuild = session.CreativeMode || instant
                ? definition.PCU
                : MyCubeBlockDefinition.PCU_CONSTRUCTION_STAGE_COST;

            if (session.IsWithinWorldLimits(out _, owner, definition.GetLimitType(), pcuToBuild, 1, builtGrid.BlocksCount) != MySession.LimitResult.Passed)
                return false;

            if (!session.SurvivalMode || instant)
                return true;

            if (!MyEntities.TryGetEntityById(builder, out MyEntity entity) || !(entity is MyCharacter || entity is IMyShipWelder))
                return false;

            var inventory = MyCubeBuilder.BuildComponent?.GetBuilderInventory(entity);
            var components = definition.Components;
            return inventory != null
                && components != null
                && components.Length != 0
                && inventory.GetItemAmount(components[0].Definition.Id) >= 1;
        }
    }
}
