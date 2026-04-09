using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Text;

public class AnimatorExporter : Editor
{
    // 在顶部菜单栏添加一个快捷入口
    [MenuItem("Tools/游戏开发助手/导出选中 Animator 为文本")]
    public static void ExportAnimator()
    {
        // 获取当前在 Project 窗口中选中的物体
        AnimatorController controller = Selection.activeObject as AnimatorController;
        
        if (controller == null)
        {
            Debug.LogWarning("[AnimatorExporter] 导出失败：请先在 Project 窗口中选中一个 Animator Controller！");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"# Animator Controller: {controller.name}");
        sb.AppendLine("=========================================\n");

        // 1. 导出所有参数 (Parameters)
        sb.AppendLine("## 1. Parameters (动画参数)");
        if (controller.parameters.Length == 0) sb.AppendLine("- (无参数)");
        foreach (var param in controller.parameters)
        {
            sb.AppendLine($"- [{param.type}] {param.name} (默认值: {GetDefaultValue(param)})");
        }
        sb.AppendLine();

        // 2. 导出所有层级与状态 (Layers & States)
        sb.AppendLine("## 2. Layers & State Machines (层级与状态机)");
        foreach (var layer in controller.layers)
        {
            sb.AppendLine($"### Layer: {layer.name} (Weight: {layer.defaultWeight})");
            DumpStateMachine(layer.stateMachine, sb, "");
            sb.AppendLine();
        }

        // 3. 复制到剪贴板
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"<color=#00FF00>[AnimatorExporter]</color> 成功！{controller.name} 的结构已复制到剪贴板，快去粘贴给 Cursor 吧！");
    }

    // 获取参数的默认值
    private static string GetDefaultValue(AnimatorControllerParameter param)
    {
        switch (param.type)
        {
            case AnimatorControllerParameterType.Float: return param.defaultFloat.ToString();
            case AnimatorControllerParameterType.Int: return param.defaultInt.ToString();
            case AnimatorControllerParameterType.Bool: return param.defaultBool.ToString();
            case AnimatorControllerParameterType.Trigger: return "N/A";
            default: return "N/A";
        }
    }

    // 递归解析状态机（支持子状态机）
    private static void DumpStateMachine(AnimatorStateMachine sm, StringBuilder sb, string indent)
    {
        // 处理 Any State 的转换
        if (sm.anyStateTransitions.Length > 0)
        {
            sb.AppendLine($"{indent}- [Any State]");
            foreach (var trans in sm.anyStateTransitions)
            {
                string dest = trans.destinationState != null ? trans.destinationState.name : "SubMachine";
                sb.AppendLine($"{indent}    => 转至: {dest}  |  条件: {GetConditions(trans)}");
            }
        }

        // 处理普通 State
        foreach (var stateNode in sm.states)
        {
            var state = stateNode.state;
            sb.AppendLine($"{indent}- [State] {state.name}");
            foreach (var trans in state.transitions)
            {
                string dest = trans.destinationState != null ? trans.destinationState.name : "Exit/SubMachine";
                sb.AppendLine($"{indent}    => 转至: {dest}  |  条件: {GetConditions(trans)}");
            }
        }

        // 处理嵌套的子状态机 (Sub-StateMachines)
        foreach (var subSmNode in sm.stateMachines)
        {
            sb.AppendLine($"{indent}- [SubStateMachine] {subSmNode.stateMachine.name}");
            DumpStateMachine(subSmNode.stateMachine, sb, indent + "    ");
        }
    }

    // 解析转换条件
    private static string GetConditions(AnimatorStateTransition trans)
    {
        string result = "";
        
        
        if (trans.conditions.Length > 0)
        {
            string[] conds = new string[trans.conditions.Length];
            for (int i = 0; i < trans.conditions.Length; i++)
            {
                var c = trans.conditions[i];
                // Unity 内部的 c.mode 会输出 Greater, Less, Equals, If, IfNot 等
                conds[i] = $"({c.parameter} {c.mode} {c.threshold})";
            }
            result = string.Join(" AND ", conds);
        }

        if (trans.hasExitTime)
        {
            result += string.IsNullOrEmpty(result) ? $"Has Exit Time ({trans.exitTime})" : $" AND Has Exit Time";
        }

        if (string.IsNullOrEmpty(result))
        {
            return "无条件 (自动过渡)";
        }

        return result;
    }
}