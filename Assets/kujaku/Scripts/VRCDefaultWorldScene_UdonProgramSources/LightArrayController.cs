using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class LightArrayController : UdonSharpBehaviour
{
    [Header("オブジェクト参照")]
    public Renderer[] lightTubes;

    [Header("発光設定")]
    public Color[] lightColors;
    public float emissionIntensity = 3.0f;

    [Header("シーケンス設定")]
    public float sequenceDelay = 0.05f;
    
    [UdonSynced, FieldChangeCallback(nameof(Mode))]
    private int _mode = 0;

    private MaterialPropertyBlock _propBlock;
    private Color _blackColor = Color.black;
    private int _sequenceIndex = 0;
    private bool _isSequenceRunning = false;
    
    public int Mode
    {
        set
        {
            _mode = value;
            UpdateVisuals();
        }
        get => _mode;
    }

    void Start()
    {
        _propBlock = new MaterialPropertyBlock();
        UpdateVisuals();
    }

    // ★★★ここが変更点です！★★★
    // オブジェクトを手に持った状態で「使用」ボタン（トリガー等）を押した時に呼び出されます。
    public override void OnPickupUseDown()
    {
        // 所有権を自分に移します。
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        // モードを次の段階へ進めます。
        // モードは現在4つ (0:消灯, 1:全点灯(色1), 2:全点灯(色2), 3:シーケンス)
        Mode = (_mode + 1) % 4; 

        // 変更したモードを全プレイヤーに同期します。
        RequestSerialization();
    }

    private void UpdateVisuals()
    {
        StopChaseSequence();

        switch (_mode)
        {
            case 0: // モード0: 全消灯
                SetAllLights(_blackColor, false);
                break;
            case 1: // モード1: 全点灯 (色リストの1番目の色)
                SetAllLights(lightColors[0] * emissionIntensity, true);
                break;
            case 2: // モード2: 全点灯 (色リストの2番目の色)
                SetAllLights(lightColors[1] * emissionIntensity, true);
                break;
            case 3: // モード3: シーケンス点灯
                SetAllLights(_blackColor, false);
                StartChaseSequence();
                break;
        }
    }

    void SetAllLights(Color color, bool emissionOn)
    {
        foreach (Renderer tube in lightTubes)
        {
            tube.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_EmissionColor", color);
            tube.SetPropertyBlock(_propBlock);

            if (emissionOn)
                tube.material.EnableKeyword("_EMISSION");
            else
                tube.material.DisableKeyword("_EMISSION");
        }
    }

    public void StartChaseSequence()
    {
        _sequenceIndex = 0;
        _isSequenceRunning = true;
        ChaseStep();
    }

    public void StopChaseSequence()
    {
        _isSequenceRunning = false;
    }

    public void ChaseStep()
    {
        if (!_isSequenceRunning) return;

        if (_sequenceIndex > 0)
        {
            ToggleLight(_sequenceIndex - 1, false);
        }
        else
        {
            ToggleLight(lightTubes.Length - 1, false);
        }

        ToggleLight(_sequenceIndex, true);
        _sequenceIndex = (_sequenceIndex + 1) % lightTubes.Length;
        SendCustomEventDelayedSeconds(nameof(ChaseStep), sequenceDelay);
    }

    void ToggleLight(int index, bool isOn)
    {
        if (index < 0 || index >= lightTubes.Length) return;

        Renderer tube = lightTubes[index];
        Color targetColor = isOn ? lightColors[2] * emissionIntensity : _blackColor;

        tube.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_EmissionColor", targetColor);
        tube.SetPropertyBlock(_propBlock);

        if (isOn)
            tube.material.EnableKeyword("_EMISSION");
        else
            tube.material.DisableKeyword("_EMISSION");
    }
}