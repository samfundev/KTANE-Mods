using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class SwitchesLogging : ModuleTweak
{
    const string typeName = "SwitchModule";

    private static int idCounter = 1;
    private readonly int moduleID;
    private readonly KMBombInfo bombInfo;

    public SwitchesLogging(BombComponent bombComponent) : base(bombComponent)
    {
        componentType = componentType ?? (componentType = ReflectionHelper.FindType(typeName));
        mGetCurrentConfiguration = componentType?.GetMethod("GetCurrentConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
        mSwitches = componentType?.GetField("Switches", BindingFlags.Public | BindingFlags.Instance);

        component = bombComponent.GetComponent(componentType);
        moduleID = idCounter++;
        bombInfo = bombComponent.GetComponent<KMBombInfo>();

        if (componentType == null || component == null || mGetCurrentConfiguration == null || mSwitches == null)
        {
            Debug.Log($"[Switches #{moduleID}] Logging failed: {new object[] { componentType, component, mGetCurrentConfiguration, mSwitches }.Select(obj => obj == null ? "<NULL>" : "(not null)").Join(", ")}.");
            return;
        }

        LogSwitches("Initial state:");

        var switches = (Array) mSwitches.GetValue(component);
        var selectables = switches.Cast<MonoBehaviour>().Select(m => m.GetComponent<KMSelectable>()).ToArray();
        for (int i = 0; i < selectables.Length; i++)
            bindInteract(selectables[i], i);

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
        {
            Debug.Log($"[Switches #{moduleID}] Module solved.");
            return true;
        };
    }

    private void bindInteract(KMSelectable sel, int i)
    {
        var prevInteract = sel.OnInteract;
        sel.OnInteract = delegate
        {
            var config = getSwitchConfiguration();
            var ret = prevInteract();
            if (getSwitchConfiguration().SequenceEqual(config))
                Debug.LogFormat($"[Switches #{moduleID}] Toggling switch {i + 1} was not allowed. Strike!");
            else
                LogSwitches($"Switch {i + 1} toggled. State now:");
            return ret;
        };
    }

    private void LogSwitches(string str)
    {
        Debug.Log($"[Switches #{moduleID}] {str} {getSwitchConfiguration()?.Select(s => s ? "Up" : "Down").Join(", ")}");
    }

    private bool[] getSwitchConfiguration()
    {
        var config = mGetCurrentConfiguration.Invoke(component, null);
        if (mSwitchStates == null)
            mSwitchStates = config.GetType().GetField("SwitchStates", BindingFlags.Public | BindingFlags.Instance);
        if (mSwitchStates == null || !typeof(bool[]).IsAssignableFrom(mSwitchStates.FieldType))
        {
            Debug.Log($"[Switches #{moduleID}] Logging failed because SwitchStates {(mSwitchStates == null ? "could not be found" : $"is of the wrong type ({mSwitchStates.FieldType.FullName})")}.");
            return null;
        }
        return (bool[]) mSwitchStates.GetValue(config);
    }

    static Type componentType;
    static MethodInfo mGetCurrentConfiguration;
    static FieldInfo mSwitches;
    static FieldInfo mSwitchStates;
}