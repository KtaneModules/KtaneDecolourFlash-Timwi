using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class DecolourFlashScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] ButtonSels;
    public TextMesh ScreenText;
    public GameObject[] ButtonObjs;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private Coroutine[] _pressAnimations = new Coroutine[2];

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ButtonSels.Length; i++)
        {
            ButtonSels[i].OnInteract += ButtonPress(i);
            ButtonSels[i].OnInteractEnded += ButtonRelease(i);
        }
    }

    private KMSelectable.OnInteractHandler ButtonPress(int i)
    {
        return delegate ()
        {
            Debug.LogFormat("[Decolour Flash #{0}] Pressed {1}.", _moduleId, i == 0 ? "YES" : "NO");
            if (_pressAnimations[i] != null)
                StopCoroutine(_pressAnimations[i]);
            _pressAnimations[i] = StartCoroutine(PressAnimation(i, true));
            if (_moduleSolved)
                return false;
            return false;
        };
    }

    private Action ButtonRelease(int i)
    {
        return delegate ()
        {
            if (_pressAnimations[i] != null)
                StopCoroutine(_pressAnimations[i]);
            _pressAnimations[i] = StartCoroutine(PressAnimation(i, false));
            if (_moduleSolved)
                return;
        };
    }

    private IEnumerator PressAnimation(int btn, bool pushIn)
    {
        var duration = 0.1f;
        var elapsed = 0f;
        var curPos = ButtonObjs[btn].transform.localPosition;
        while (elapsed < duration)
        {
            ButtonObjs[btn].transform.localPosition = new Vector3(curPos.x, Easing.InOutQuad(elapsed, curPos.y, pushIn ? 0.01f : 0.0146f, duration), curPos.z);
            yield return null;
            elapsed += Time.deltaTime;
        }
    }
}
