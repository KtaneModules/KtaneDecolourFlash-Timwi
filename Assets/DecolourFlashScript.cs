using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    private readonly Dictionary<Hex, ColourInfo> _hexes = new Dictionary<Hex, ColourInfo>();

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
            _hexes[hexes[i]] = new ColourInfo(_colors[grid[i] % 6], (CFColour) (grid[i] % 6), (CFColour) (grid[i] / 6));

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
        Debug.LogFormat("[Decolour Flash #{0}] Start position is {1}.", _moduleId, _startPos.Select(hex => string.Format("{0} {1}", _hexes[hex], hex)).Join(", "));
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
            ShowColor(ix == 3 ? (ColourInfo?) null : _hexes[_goals[ix]]);
            return;
        }

        ShowColor(_stage == 5 ? (ColourInfo?) null : _hexes[_currentPos[GetCurrentIndex()]]);
    }

    private int GetCurrentIndex()
    {
        // Returns the index within ‘_goal/_currentPos’ which is currently flashing.
        return (int) (Time.time / _flashSpeed) % (_stage == 0 ? 4 : 3);
    }

    private void ShowColor(ColourInfo? colorInfo)
    {
        ScreenText.text = colorInfo == null ? "" : colorInfo.Value.Word.ToString().ToUpperInvariant();
        if (colorInfo != null)
            ScreenText.color = colorInfo.Value.Colour;
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
            Audio.PlaySoundAtTransform("InputCorrect", transform);
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
                var result = MakeMove(_currentPos, GetCurrentIndex());
                if (result == MoveResult.HitEdge)
                {
                    Debug.LogFormat("[Decolour Flash #{0}] You attempted to go over the edge of the diagram. Strike!", _moduleId);
                    Module.HandleStrike();
                }
                else if (result == MoveResult.HitCenter && _stage == 4)
                {
                    Debug.LogFormat("[Decolour Flash #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    _stage++;
                }
                else if (result == MoveResult.HitCenter)
                {
                    Debug.LogFormat("[Decolour Flash #{0}] You attempted to hit the centre of the diagram before you are done. Strike!", _moduleId);
                    Module.HandleStrike();
                }
                break;
        }
    }

    private static MoveResult MakeMove(List<Hex> position, int curIx)
    {
        var curHex = position[curIx];
        position.RemoveAt(curIx);
        var opposite = Hex.LargeHexagon(4).Where(h => h != curHex && position.All(cp => h.Neighbors.Contains(cp))).ToArray();
        if (opposite.Length == 0)
        {
            position.Insert(curIx, curHex);
            return MoveResult.HitEdge;
        }
        else if (opposite.Length == 1 && opposite[0].Q == 0 && opposite[0].R == 0)
        {
            position.Insert(curIx, curHex);
            return MoveResult.HitCenter;
        }
        position.Add(opposite[0]);
        return MoveResult.Success;
    }

    enum MoveResult { Success, HitEdge, HitCenter }

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

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press Yes/No | !{0} press Yes Blue Magenta | !{0} NBM,NYW,YRG [press No when the word Blue is shown in Magenta colour, then No on Yellow in White, then Yes on Red in Green] | !{0} reset";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        var cmdRegex = @"(?:yes|y|no|n)(?:\s*(?:blue|b|green|g|red|r|magenta|m|yellow|y|white|w)\s*(?:blue|b|green|g|red|r|magenta|m|yellow|y|white|w))?";
        if ((m = Regex.Match(command,
            string.Format(@"^\s*(?:(?:press|hit|submit)\s+)?({0}(?:,{0})*)\s*$", cmdRegex),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var cmds = new List<TpCommandInfo>();

            foreach (var subcmd in m.Groups[1].Value.Split(','))
            {
                CFColour? colour = null, word = null;

                var m2 = Regex.Match(subcmd,
                    @"^\s*((?:press|hit|submit)\s+)?(?:(?<y>yes|y)|no|n)(?:\s*(?<w>blue|b|green|g|red|r|magenta|m|yellow|y|white|w)\s*(?<c>blue|b|green|g|red|r|magenta|m|yellow|y|white|w))?\s*$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!m2.Success)
                    yield break;
                if (m2.Groups["w"].Success)
                {
                    const string colourChars = "bgrmyw";
                    word = (CFColour) colourChars.IndexOf(char.ToLowerInvariant(m2.Groups["w"].Value[0]));
                    colour = (CFColour) colourChars.IndexOf(char.ToLowerInvariant(m2.Groups["c"].Value[0]));
                }
                cmds.Add(new TpCommandInfo(m2.Groups["y"].Success, word, colour));
            }

            yield return null;

            var numProcessed = 0;
            foreach (var tpCmd in cmds)
            {
                if (tpCmd.Colour != null)
                {
                    var colorIx = _stage == 0
                        ? _goals.IndexOf(g => _hexes[g].ColourIx == tpCmd.Colour.Value && _hexes[g].Word == tpCmd.Word.Value)
                        : _currentPos.IndexOf(g => _hexes[g].ColourIx == tpCmd.Colour && _hexes[g].Word == tpCmd.Word.Value);
                    if (colorIx == -1)
                    {
                        yield return string.Format("sendtochaterror The combination “{0} on {1}” is not displayed on the module ({2} commands were processed).",
                            tpCmd.Word.Value.ToString().ToUpperInvariant(), tpCmd.Colour.Value.ToString().ToUpperInvariant(), numProcessed);
                        yield break;
                    }

                    // Wait a cycle to avoid the situation where the button is pressed on the correct colour combination but released on the next one (the handler runs on button release)
                    while (GetCurrentIndex() == colorIx)
                        yield return null;
                    while (GetCurrentIndex() != colorIx)
                        yield return null;
                }

                yield return new[] { ButtonSels[tpCmd.YesButton ? 0 : 1] };
                numProcessed++;
            }
        }
        else if (Regex.IsMatch(command, @"^\s*reset\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            yield return ButtonSels[1];
            while (_holdRoutine != null)
                yield return null;
            yield return ButtonSels[1];
        }
    }

    public IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat("<> TP");

        if (_stage == 0)
        {
            ButtonSels[0].OnInteract();
            yield return new WaitForSeconds(.1f);
            ButtonSels[0].OnInteractEnded();
            yield return new WaitForSeconds(.1f);
        }

        while (_stage < 5)
        {
            var q = new Queue<QueueItem>();
            var visited = new Dictionary<long, QueueItem>();
            q.Enqueue(new QueueItem { GridPosition = _currentPos.ToArray(), Num = PosToNum(_currentPos), Parent = -1 });
            var goalNum = -1L;
            Hex goalHex = default(Hex);
            while (q.Count > 0)
            {
                var item = q.Dequeue();
                if (visited.ContainsKey(item.Num))
                    continue;
                visited[item.Num] = item;
                if (_stage < 4 && item.GridPosition.Contains(_goals[_stage - 1]))
                {
                    goalNum = item.Num;
                    break;
                }

                for (var moveIx = 0; moveIx < 3; moveIx++)
                {
                    var pos = item.GridPosition.ToList();
                    var result = MakeMove(pos, moveIx);
                    if (result == MoveResult.Success)
                        q.Enqueue(new QueueItem { GridPosition = pos.ToArray(), HexPressed = item.GridPosition[moveIx], Num = PosToNum(pos), Parent = item.Num });
                    else if (_stage == 4 && result == MoveResult.HitCenter)
                    {
                        goalNum = item.Num;
                        goalHex = item.GridPosition[moveIx];
                        break;
                    }
                }
            }

            var p = visited[goalNum];
            var moves = new List<Hex>();
            while (p.Parent != -1)
            {
                var parent = visited[p.Parent];
                moves.Add(p.HexPressed);
                p = parent;
            }

            for (int i = moves.Count - 1; i >= 0; i--)
            {
                var move = moves[i];
                Debug.LogFormat("<> Current position: {0}; trying to make move: {1}", _currentPos.Join(", "), move);
                var ix = _currentPos.IndexOf(move);
                while (GetCurrentIndex() != ix)
                    yield return true;
                ButtonSels[1].OnInteract();
                ButtonSels[1].OnInteractEnded();
                yield return new WaitForSeconds(.1f);
            }

            var goalIx = _currentPos.IndexOf(_stage < 4 ? _goals[_stage - 1] : goalHex);
            while (GetCurrentIndex() != goalIx)
                yield return true;

            ButtonSels[_stage < 4 ? 0 : 1].OnInteract();
            ButtonSels[_stage < 4 ? 0 : 1].OnInteractEnded();
            yield return new WaitForSeconds(.1f);
        }
    }

    struct QueueItem
    {
        public Hex[] GridPosition;
        public long Num;
        public long Parent;
        public Hex HexPressed;
    }

    private long PosToNum(IEnumerable<Hex> ci)
    {
        return ci.Aggregate(0L, (p, n) => p | (1L << _hexes[n].Index));
    }

    private List<Hex> NumToPos(long num)
    {
        var cis = new List<Hex>();
        for (var bit = 0; bit < 36; bit++)
            if ((num & (1L << bit)) != 0)
                cis.Add(_hexes.First(kvp => kvp.Value.Index == bit).Key);
        return cis;
    }
}
