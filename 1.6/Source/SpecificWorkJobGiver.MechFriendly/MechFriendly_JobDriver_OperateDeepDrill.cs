using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SpecificWorkJobGiver.MechFriendly;

[HarmonyPatch]
public static class MechFriendly_JobDriver_OperateDeepDrill
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(AccessTools.Inner(typeof(JobDriver_OperateDeepDrill), "<>c__DisplayClass1_0"), "<MakeNewToils>b__1");
	}

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var code = new List<CodeInstruction>(instructions);

        if (ModLister.GetActiveModWithIdentifier("Porio.TunnulerFix") != null)
            return code;

        MethodInfo learnMethod = AccessTools.Method(typeof(Pawn_SkillTracker), "Learn");

        int skillsIdx = code.FindIndex(c => c.opcode == OpCodes.Ldfld && c.operand is FieldInfo fi && fi.Name == "skills");
        int learnIdx = skillsIdx < 0 ? -1 : code.FindIndex(skillsIdx, c => c.Calls(learnMethod));

        if (skillsIdx < 0 || learnIdx < 0 || learnIdx + 1 >= code.Count)
        {
            Log.Warning("[MechFriendly] Making Deep-drills Mech friendly failed");
            return code;
        }

        // RimWorld 1.7 already compiles this as skills?.Learn(...) (ldfld skills; dup; brtrue), nothing to do
        if (code[skillsIdx + 1].opcode == OpCodes.Dup)
            return code;

        // skills != null: continue with the Learn call; skills == null: drop it and skip past the Learn call
        Label postEditLabel = il.DefineLabel();
        Label postLearnLabel = il.DefineLabel();
        code[skillsIdx + 1].labels.Add(postEditLabel);
        code[learnIdx + 1].labels.Add(postLearnLabel);

        code.InsertRange(skillsIdx + 1, new[]
        {
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brtrue, postEditLabel),
            new CodeInstruction(OpCodes.Pop),
            new CodeInstruction(OpCodes.Br, postLearnLabel),
        });

        return code;
    }

}
