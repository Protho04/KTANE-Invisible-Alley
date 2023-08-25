using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;

/// <summary>
/// On the Subject of Invisible Alley
/// Created by Protho04
/// Assets by Timwi
/// </summary>
public class InvisibleAlleyModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] Regions;
    public KMRuleSeedable RuleSeedable;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private int[] _solution;
    private int _presses, _missing;
    private bool _isSolved;

    static T[] NewArray<T>(params T[] arr)
    {
        return arr;
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        var conditions = NewArray(
            new KeyValuePair<string, Func<bool>>("> 0 vowels in SN", () => Bomb.GetSerialNumber().Any(l => "AEIOU".ContainsIgnoreCase(l.ToString()))),
            new KeyValuePair<string, Func<bool>>("< 1 vowel in SN", () => !Bomb.GetSerialNumber().Any(l => "AEIOU".ContainsIgnoreCase(l.ToString()))),
            new KeyValuePair<string, Func<bool>>("> 1 unlit indicator", () => Bomb.GetOffIndicators().Count() > 1),
            new KeyValuePair<string, Func<bool>>("> 1 lit indicator", () => Bomb.GetOnIndicators().Count() > 1),
            new KeyValuePair<string, Func<bool>>("< 2 unlit indicators", () => Bomb.GetOffIndicators().Count() < 2),
            new KeyValuePair<string, Func<bool>>("< 2 lit indicators", () => Bomb.GetOnIndicators().Count() < 2),
            new KeyValuePair<string, Func<bool>>("> 2 indicators", () => Bomb.GetIndicators().Count() > 2),
            new KeyValuePair<string, Func<bool>>("< 3 indicators", () => Bomb.GetIndicators().Count() < 3),
            new KeyValuePair<string, Func<bool>>("> 0 D Batteries", () => Bomb.GetBatteryCount(Battery.D) > 0),
            new KeyValuePair<string, Func<bool>>("> 0 AA Batteries", () => Bomb.GetBatteryCount(Battery.AA) > 0),
            new KeyValuePair<string, Func<bool>>("< 2 D Batteries", () => Bomb.GetBatteryCount(Battery.D) < 2),
            new KeyValuePair<string, Func<bool>>("< 4 AA Batteries", () => Bomb.GetBatteryCount(Battery.AA) < 4),
            new KeyValuePair<string, Func<bool>>("> 3 Batteries", () => Bomb.GetBatteryCount() > 3),
            new KeyValuePair<string, Func<bool>>("< 5 Batteries", () => Bomb.GetBatteryCount() < 5),
            new KeyValuePair<string, Func<bool>>("> 0 Batteries", () => Bomb.GetBatteryCount() > 0),
            new KeyValuePair<string, Func<bool>>("> 2 port plates", () => Bomb.GetPortPlateCount() > 2),
            new KeyValuePair<string, Func<bool>>("< 3 port plates", () => Bomb.GetPortPlateCount() < 3),
            new KeyValuePair<string, Func<bool>>("> 0 DVI-D ports", () => Bomb.IsPortPresent(Port.DVI)),
            new KeyValuePair<string, Func<bool>>("> 0 parallel ports", () => Bomb.IsPortPresent(Port.Parallel)),
            new KeyValuePair<string, Func<bool>>("> 0 PS/2 ports", () => Bomb.IsPortPresent(Port.PS2)),
            new KeyValuePair<string, Func<bool>>("> 0 RJ-45 ports", () => Bomb.IsPortPresent(Port.RJ45)),
            new KeyValuePair<string, Func<bool>>("> 0 serial ports", () => Bomb.IsPortPresent(Port.Serial)),
            new KeyValuePair<string, Func<bool>>("> 0 stereo RCA ports", () => Bomb.IsPortPresent(Port.StereoRCA)),
            new KeyValuePair<string, Func<bool>>("> 0 empty port plates", () => Bomb.GetPortPlates().Any(pl => pl.Length == 0)),
            new KeyValuePair<string, Func<bool>>("< 1 empty port plates", () => !Bomb.GetPortPlates().Any(pl => pl.Length == 0))
        );
        var dirs = NewArray(
            NewArray('R', 'D'),
            NewArray('D', 'L'),
            NewArray('U', 'R', 'D'),
            NewArray('U', 'R', 'D', 'L'),
            NewArray('D', 'L'),
            NewArray('U', 'R'),
            NewArray('U', 'R', 'L'),
            NewArray('U', 'L'));
        var dirs2 = new char[8][];
        int[] cumulativeOffsets = NewArray(0, 2, 4, 7, 11, 13, 15, 18);

        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[Invisible Alley #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);

        for (int i = 0; i < 8; i++)
        {
            dirs[i] = dirs[i].OrderBy(_ => rnd.NextDouble()).ToArray();
            dirs2[i] = dirs[i].OrderBy(_ => rnd.NextDouble()).ToArray();
        }

        conditions = conditions.OrderBy(_ => rnd.NextDouble()).ToArray();

        _missing = UnityEngine.Random.Range(0, 7);
        if (_missing >= 3)
            _missing++;
        Regions[_missing].gameObject.SetActive(false);
        var names = new[] { "TL", "TM", "ML", "MC", "MR", "BL", "BM", "BR" };
        var dirnames = new Dictionary<char, string>() { { 'U', "up" }, { 'R', "right" }, { 'D', "down" }, { 'L', "left" } };
        Debug.LogFormat("[Invisible Alley #{0}] Missing region: {1}", _moduleId, names[_missing]);

        List<int> sol = new List<int>();
        bool[] used = Enumerable.Repeat(false, 8).ToArray();
        bool[] ignore = used.ToArray();
        ignore[_missing] = true;
        int cur = _missing;
        Debug.LogFormat("[Invisible Alley #{0}] Begin at {1}.", _moduleId, names[_missing]);
        while (true)
        {
            for (int dir = 0; dir < dirs[cur].Length; dir++)
            {
                if (conditions[cumulativeOffsets[cur] + dir].Value())
                {
                    if (!ignore[Move(cur, dirs[cur][dir])])
                    {
                        if (!used[Move(cur, dirs[cur][dir])])
                        {
                            Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2} {3}) applies. Move {4} (to {5}).",
                                _moduleId,
                                dir + 1,
                                dirs[cur][dir],
                                conditions[cumulativeOffsets[cur] + dir].Key,
                                dirnames[dirs[cur][dir]],
                                names[Move(cur, dirs[cur][dir])]);
                            cur = Move(cur, dirs[cur][dir]);
                            used[cur] = true;
                            goto cont;
                        }
                        else
                        {
                            Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2} {3}) applies that region but has already been visited.", _moduleId, dir + 1, dirs[cur][dir], conditions[cumulativeOffsets[cur] + dir].Key);
                        }
                    }
                    else
                    {
                        Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2} {3}) applies but that region {4}.", _moduleId, dir + 1, dirs[cur][dir], conditions[cumulativeOffsets[cur] + dir].Key, _missing != Move(cur, dirs[cur][dir]) ? "has already been pressed" : "does not exist");
                    }
                }
                else
                {
                    Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2} {3}) does not apply.", _moduleId, dir + 1, dirs[cur][dir], conditions[cumulativeOffsets[cur] + dir].Key);
                }
            }
            for (int dir = 0; dir < dirs2[cur].Length; dir++)
            {
                if (!ignore[Move(cur, dirs2[cur][dir])])
                {
                    if (!used[Move(cur, dirs2[cur][dir])])
                    {
                        Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2}) applies. Move {3} (to {4}).",
                            _moduleId,
                            dir + dirs2[cur].Length + 1,
                            dirs2[cur][dir],
                            dirnames[dirs2[cur][dir]],
                            names[Move(cur, dirs2[cur][dir])]);
                        cur = Move(cur, dirs2[cur][dir]);
                        used[cur] = true;
                        goto cont;
                    }
                    else
                    {
                        Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2}) applies but that region has already been visited", _moduleId, dir + dirs2[cur].Length + 1, dirs2[cur][dir], dirnames[dirs2[cur][dir]]);
                    }
                }
                else
                {
                    Debug.LogFormat("[Invisible Alley #{0}] Rule {1} ({2}) applies but that region {3}.", _moduleId, dir + dirs2[cur].Length + 1, dirs2[cur][dir], _missing != Move(cur, dirs2[cur][dir]) ? "has already been pressed" : "does not exist");
                }
            }
            if (ignore[cur])
                break;
            sol.Add(cur);
            ignore[cur] = transform;
            Debug.LogFormat("[Invisible Alley #{0}] No further movements can be made, so you must press {1} next.", _moduleId, names[cur]);
            var pressed = Enumerable.Range(0, 8).Where(i => ignore[i] && i != _missing).Select(i => names[i]).ToArray();
            string f = "";
            if (pressed.Length == 1)
                f = pressed[0];
            else if (pressed.Length == 2)
                f = pressed[0] + " and " + pressed[1];
            else
                f = pressed.Take(pressed.Length - 1).Join(", ") + ", and " + pressed.Last();
            Debug.LogFormat("[Invisible Alley #{0}] Begin again, starting at {1}. {2} doesn't exist and you treat {3} as though they don't either.", _moduleId, names[cur], names[_missing], f);
            used = Enumerable.Repeat(false, 8).ToArray();

            cont:;
        }
        Debug.LogFormat("[Invisible Alley #{0}] No further movements can be made, but this is the same region you started at, so you're done.", _moduleId);

        _solution = sol.ToArray();
        Debug.LogFormat("[Invisible Alley #{0}] Must press these regions in order: {1}", _moduleId, _solution.Select(i => names[i]).Join(", "));

        for (int i = 0; i < 8; i++)
        {
            if (i == _missing)
                continue;
            int j = i;
            Regions[j].OnInteract += () =>
            {
                Regions[j].AddInteractionPunch(0.1f);
                if (_isSolved)
                    return false;

                if (_solution[_presses] == j)
                {
                    _presses++;
                    Debug.LogFormat("[Invisible Alley #{0}] Correctly pressed {1}.", _moduleId, names[j]);
                    if (_presses == _solution.Length)
                    {
                        Debug.LogFormat("[Invisible Alley #{0}] That's every region. Solved!", _moduleId);
                        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                        Module.HandlePass();
                        _isSolved = true;
                    }
                }
                else
                {
                    Debug.LogFormat("[Invisible Alley #{0}] Inorrectly pressed {1} (I was expecting {2}). Strike! Input reset.", _moduleId, names[j], names[_solution[_presses]]);
                    _presses = 0;
                    Module.HandleStrike();
                }
                return false;
            };
        }
    }

    private static int Move(int cur, char v)
    {
        switch (v)
        {
            case 'L':
                return cur - 1;
            case 'R':
                return cur + 1;
            case 'U':
                return cur - (cur > 3 ? 3 : 2);
            case 'D':
                return cur + (cur > 1 ? 3 : 2);
        }

        throw new Exception("Unreachable");
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} TL, TM [top-left, top-middle, etc.] !{0} which [find out which region is missing]";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        var split = command.Trim().ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
            yield break;
        if (split.Length == 1 &&
            (split[0].Equals("which", StringComparison.InvariantCulture | StringComparison.CurrentCulture) ||
            split[0].Equals("look", StringComparison.InvariantCulture | StringComparison.CurrentCulture) ||
            split[0].Equals("cycle", StringComparison.InvariantCulture | StringComparison.CurrentCulture) ||
            split[0].Equals("w", StringComparison.InvariantCulture | StringComparison.CurrentCulture) ||
            split[0].Equals("l", StringComparison.InvariantCulture | StringComparison.CurrentCulture) ||
            split[0].Equals("c", StringComparison.InvariantCulture | StringComparison.CurrentCulture)))
        {
            yield return null;
            var names = new[] { "TL", "TM", "ML", "MC", "MR", "BL", "BM", "BR" };
            yield return "sendtochat {0}, the missing region on Invisible Alley (!{1}) is " + names[_missing] + ".";
            yield break;
        }
        var skip = split[0].Equals("press", StringComparison.InvariantCulture | StringComparison.CurrentCulture) ? 1 : 0;
        if (!split.Skip(skip).Any())
            yield break;

        var btns = new List<int>();
        foreach (var cmd in split.Skip(skip))
            switch (cmd.Replace("center", "middle").Replace("centre", "middle"))
            {
                case "tl": case "lt": case "topleft": case "lefttop": btns.Add(0); break;
                case "tm": case "tc": case "mt": case "ct": case "topmiddle": case "middletop": btns.Add(1); break;

                case "ml": case "cl": case "lm": case "lc": case "middleleft": case "leftmiddle": btns.Add(2); break;
                case "mm": case "cm": case "mc": case "cc": case "middle": case "middlemiddle": btns.Add(3); break;
                case "mr": case "cr": case "rm": case "rc": case "middleright": case "rightmiddle": btns.Add(4); break;

                case "bl": case "lb": case "bottomleft": case "leftbottom": btns.Add(5); break;
                case "bm": case "bc": case "mb": case "cb": case "bottommiddle": case "middlebottom": btns.Add(6); break;
                case "br": case "rb": case "bottomright": case "rightbottom": btns.Add(7); break;

                default: yield break;
            }
        if (btns.Contains(_missing))
            yield break;
        yield return null;
        foreach (int j in btns)
        {
            Regions[j].OnInteract();
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!_isSolved)
        {
            Regions[_solution[_presses]].OnInteract();
            if (!_isSolved)
                yield return new WaitForSeconds(0.1f);
        }
    }
}
