using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DecolourFlash;
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
    public KMRuleSeedable RuleSeedable;

    private int _moduleId;
    private static int _moduleIdCounter = 1;

    private readonly Coroutine[] _pressAnimations = new Coroutine[2];
    private readonly Hex[] _goals = new Hex[3];
    private int _stage; // 0 = showing goals; 1–3 = normal operation; 4 = solved
    private readonly List<Hex> _currentPos = new List<Hex>();
    private readonly List<Hex> _startPos = new List<Hex>();
    private Coroutine _holdRoutine;

    private static readonly Color[] _colors = { Color.blue, Color.green, Color.red, Color.magenta, Color.yellow, Color.white };
    private static readonly string[] _colorNames = { "blue", "green", "red", "magenta", "yellow", "white" };

    private readonly Dictionary<Hex, ColorInfo> _hexes = new Dictionary<Hex, ColorInfo>();

    const float _flashSpeed = .75f;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ButtonSels.Length; i++)
        {
            ButtonSels[i].OnInteract += ButtonPress(i);
            ButtonSels[i].OnInteractEnded += ButtonRelease(i);
        }

        // Rule seed
        var rnd = RuleSeedable.GetRNG();
        // Random spread to make Rule Seed 1 reasonably spread-out (no same-color clumps)
        rnd.Next(0, 2);
        rnd.Next(0, 2);
        var hexes = Hex.LargeHexagon(4).Where(h => h.Distance > 0).ToList();
        var grid = new List<int>();
        Func<bool> findGrid = null;
        findGrid = () =>
        {
            if (grid.Count == hexes.Count)
                return true;

            var ofs = rnd.Next(0, 36);
            for (var combR = 0; combR < 36; combR++)
            {
                var comb = (combR + ofs) % 36;
                if (grid.Contains(comb))
                    continue;
                var valid = true;
                for (var prevIx = 0; prevIx < grid.Count && valid; prevIx++)
                    if (adjacent(hexes[prevIx], hexes[grid.Count]) && (grid[prevIx] % 6 == comb % 6 || grid[prevIx] / 6 == comb / 6))
                        valid = false;
                if (!valid)
                    continue;

                grid.Add(comb);
                var success = findGrid();
                if (success)
                    return true;
                grid.RemoveAt(grid.Count - 1);
            }
            return false;
        };
        findGrid();
        for (var i = 0; i < hexes.Count; i++)
            _hexes[hexes[i]] = new ColorInfo(_colors[grid[i] % 6], _colorNames[grid[i] % 6], (CFColour) (grid[i] / 6));

        // Initialize module
        hexes = Hex.LargeHexagon(4).Where(h => h.Distance > 0).ToList();
        for (var i = 0; i < 3; i++)
        {
            var ix = Rnd.Range(0, hexes.Count);
            _goals[i] = hexes[ix];
            hexes.RemoveAt(ix);
            Debug.LogFormat("[Decolour Flash #{0}] Goal #{1} is {2} at {3}.", _moduleId, i + 1, _hexes[_goals[i]], _goals[i]);
        }

        // Construct the starting position
        var rndHex = hexes.Where(h => !_goals.Any(g => g.Neighbors.Contains(h))).PickRandom();
        _startPos.Add(rndHex);
        hexes.Remove(rndHex);
        var rndHex2 = hexes.Where(h => rndHex.Neighbors.Contains(h)).PickRandom();
        _startPos.Add(rndHex2);
        hexes.Remove(rndHex2);
        var rndHex3 = hexes.Where(h => rndHex.Neighbors.Contains(h) && rndHex2.Neighbors.Contains(h)).PickRandom();
        _startPos.Add(rndHex3);

        _currentPos.AddRange(_startPos);
        Debug.LogFormat("[Decolour Flash #{0}] Start position is {1}.", _moduleId, _startPos.Join(", "));
    }

    private bool adjacent(Hex hex1, Hex hex2)
    {
        return (hex2 - hex1).Distance == 1;
    }

    private void Update()
    {
        if (_stage == 0)
        {
            var ix = (int) (Time.time / _flashSpeed) % 4;
            ShowColor(ix == 3 ? (ColorInfo?) null : _hexes[_goals[ix]]);
            return;
        }

        ShowColor(_stage == 5 ? (ColorInfo?) null : _hexes[_currentPos[GetCurrentIndex()]]);
    }

    private static int GetCurrentIndex()
    {
        // Returns the index within ‘_currentPos’ which is currently flashing.
        return (int) (Time.time / _flashSpeed) % 3;
    }

    private void ShowColor(ColorInfo? colorInfo)
    {
        ScreenText.text = colorInfo == null ? "" : colorInfo.Value.Word.ToString().ToUpperInvariant();
        if (colorInfo != null)
            ScreenText.color = colorInfo.Value.Color;
    }

    private KMSelectable.OnInteractHandler ButtonPress(int i)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ButtonSels[i].transform);
            ButtonSels[i].AddInteractionPunch(.5f);
            if (_pressAnimations[i] != null)
                StopCoroutine(_pressAnimations[i]);
            _pressAnimations[i] = StartCoroutine(PressAnimation(i, true));
            if (i == 1)
                _holdRoutine = StartCoroutine(HoldButton());
            return false;
        };
    }

    private IEnumerator HoldButton()
    {
        yield return new WaitForSeconds(1f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, ButtonSels[1].transform);
        _holdRoutine = null;
    }

    private Action ButtonRelease(int i)
    {
        return delegate ()
        {
            if (_pressAnimations[i] != null)
                StopCoroutine(_pressAnimations[i]);
            _pressAnimations[i] = StartCoroutine(PressAnimation(i, false));

            if (i == 0)
                YesButtonPress();
            else
                NoButtonPress();
        };
    }

    private void YesButtonPress()
    {
        if (_stage == 0)
        {
            NoButtonPress();
            return;
        }

        if (_stage == 5)
            return;

        // Verify that the hex currently displayed is the current next goal
        if (_currentPos[GetCurrentIndex()] == _goals[_stage - 1])
        {
            Debug.LogFormat("[Decolour Flash #{0}] Achieved goal #{1}.", _moduleId, _stage);
            _stage++;
        }
        else
        {
            Debug.LogFormat("[Decolour Flash #{0}] Pressed ‘YES’ on {1} (at {2}), but the current goal is {3} (at {4}). Strike!", _moduleId,
                _hexes[_currentPos[GetCurrentIndex()]],
                _currentPos[GetCurrentIndex()],
                _hexes[_goals[_stage - 1]],
                _goals[_stage - 1]);
            Module.HandleStrike();
        }
    }

    private void NoButtonPress()
    {
        switch (_stage)
        {
            case 5: return;  // module already solved

            case 0: // we are currently showing the goal hexes
                _stage++;
                return;

            default:
                if (_holdRoutine == null)
                {
                    // Reset!
                    _currentPos.Clear();
                    _currentPos.AddRange(_startPos);
                    _stage = 0;
                    break;
                }
                StopCoroutine(_holdRoutine);

                // Find the hex on the other side of the current triangle.
                var curIx = GetCurrentIndex();
                var curHex = _currentPos[curIx];
                _currentPos.RemoveAt(curIx);
                var opposite = Hex.LargeHexagon(4).Where(h => h != curHex && _currentPos.All(cp => h.Neighbors.Contains(cp))).ToArray();
                if (opposite.Length == 0)
                {
                    Debug.LogFormat("[Decolour Flash #{0}] You attempted to go over the edge of the diagram. Strike!", _moduleId);
                    Module.HandleStrike();
                    _currentPos.Insert(curIx, curHex);
                }
                else if (opposite.Length == 1 && opposite[0].Q == 0 && opposite[0].R == 0 && _stage == 4)
                {
                    Debug.LogFormat("[Decolour Flash #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    _stage++;
                }
                else if (opposite.Length == 1 && opposite[0].Q == 0 && opposite[0].R == 0)
                {
                    Debug.LogFormat("[Decolour Flash #{0}] You attempted to hit the centre of the diagram before you are done. Strike!", _moduleId);
                    Module.HandleStrike();
                    _currentPos.Insert(curIx, curHex);
                }
                else
                    _currentPos.Add(opposite[0]);
                break;
        }
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
