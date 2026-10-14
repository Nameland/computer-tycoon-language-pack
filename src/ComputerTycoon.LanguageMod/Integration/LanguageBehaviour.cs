using UnityEngine;

namespace ComputerTycoon.LanguageMod;

public sealed class LanguageBehaviour : MonoBehaviour
{
    public LanguageBehaviour(IntPtr pointer)
        : base(pointer) { }

    public void Update() => Runtime.Update();

    public void LateUpdate() => Runtime.FinishLanguageMenuActivation();

    public void OnGUI() => Runtime.DrawGui();

    public void OnDisable() => Runtime.CloseLanguageWindow();

    public void OnDestroy() => Runtime.CloseLanguageWindow();
}
