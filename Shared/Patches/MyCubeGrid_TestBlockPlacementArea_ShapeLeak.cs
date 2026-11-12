using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Havok;
using HarmonyLib;
using MultigridProjector.Utilities;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;

namespace MultigridProjector.Patches
{
    // Fixes a Havok memory leak in the game's block placement test.
    //
    // MyCubeGrid.TestBlockPlacementArea builds a temporary list shape for blocks which are not a
    // standalone box (slopes, corners, etc.). The child shapes are created fresh by
    // MyCubeBlockCollector.AddConvexShape, but the list is created with HkReferencePolicy.None,
    // so the list adds its own reference and only that one is released afterwards. The original
    // reference of each child is never released, so every such placement test leaks Havok memory.
    //
    // Projected block build checks with Havok intersections (ship welders, hand welders, the
    // client's Ship Welding feature) call this test many times per second on damaged ships,
    // which filled the 2 GB Havok heap of the client within minutes.
    //
    // The fix passes HkReferencePolicy.TakeOwnership for that list, as the game itself does for
    // freshly created child shapes elsewhere (MyCharacterProxy, MyUseObjectsComponent), so the
    // children are released together with the list.
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [HarmonyPatch]
    public static class MyCubeGrid_TestBlockPlacementArea_ShapeLeak
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(MyCubeGrid))
                .Where(m => m.Name == nameof(MyCubeGrid.TestBlockPlacementArea))
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        [Everywhere]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();

            var addConvexShape = AccessTools.Method(typeof(MyCubeBlockCollector), nameof(MyCubeBlockCollector.AddConvexShape), new[]
            {
                typeof(VRageMath.Vector3I),
                typeof(VRageMath.Vector3I),
                typeof(Sandbox.Definitions.MyCubeBlockDefinition),
                typeof(VRageMath.MyBlockOrientation),
                typeof(float),
                typeof(List<HkShape>),
            });
            var listShapeCtor = AccessTools.Constructor(typeof(HkListShape), new[] { typeof(HkShape[]), typeof(int), typeof(HkReferencePolicy) });

            var patched = false;
            var afterAddConvexShape = false;
            for (var i = 0; i < code.Count; i++)
            {
                var instruction = code[i];
                if (!afterAddConvexShape)
                {
                    afterAddConvexShape = addConvexShape != null && instruction.Calls(addConvexShape);
                    continue;
                }

                if (instruction.opcode != OpCodes.Newobj || !Equals(instruction.operand, listShapeCtor))
                    continue;

                // The policy argument is loaded right before the constructor call
                var policy = code[i - 1];
                if (policy.opcode == OpCodes.Ldc_I4_1 || (policy.opcode == OpCodes.Ldc_I4 && (int)policy.operand == (int)HkReferencePolicy.None))
                {
                    // Change in place to keep any labels and blocks on the instruction
                    policy.opcode = OpCodes.Ldc_I4;
                    policy.operand = (int)HkReferencePolicy.TakeOwnership;
                    patched = true;
                }

                break;
            }

            if (!patched)
                PluginLog.Warn("MyCubeGrid_TestBlockPlacementArea_ShapeLeak: Code pattern not found, the Havok shape leak fix is not applied (game code changed?)");

            return code;
        }
    }
}
