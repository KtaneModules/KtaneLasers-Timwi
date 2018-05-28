using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;

public static class GeneralExtensions
{
    public static bool EqualsAny(this object obj, params object[] targets)
    {
        return targets.Contains(obj);
    }
}

public class LasersModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] pipes;
    public Renderer[] Connections, centerPipes;
    public TextMesh[] numbers;
    static Color orange = new Color(1f, 140/255f, 0), purple = new Color(0.5f, 0, 0.5f);

    private List<int> pipeOrder = new List<int>(), combination = new List<int>();
    private bool canPress = false, isStriking = true, Diagonal, special, activated;
    private Color a;
    private List<Renderer> remember = new List<Renderer>(), startOff = new List<Renderer>();
    private List<Color> colorList = new List<Color> { Color.red, orange, Color.yellow, Color.green, Color.blue, purple, Color.white };
    private int r;
    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private int rowRoot, columnRoot, timeRoot, originalTime, moduleParity;
    private string message;
    private Dictionary<Renderer, Renderer> pipeConnections = new Dictionary<Renderer, Renderer>();
    private List<List<Renderer>> pipeRows = new List<List<Renderer>>(), pipeColumns = new List<List<Renderer>>();

    void Start()
    {
        for (int i = 0; i < Connections.Length; i++)
        {
            if (i < 12)
            {
                Connections[i].material.color = Color.red;
            }
            else
            {
                Connections[i].gameObject.SetActive(false);
                startOff.Add(Connections[i]);
            }
        }
        for (int i = 0; i < 3; i++)
        {
            pipeColumns.Add(new Renderer[3].ToList());
            pipeRows.Add(new Renderer[3].ToList());
        }
        for (int i = 0; i < centerPipes.Count(); i++)
        {
            centerPipes[i].material.color = Color.red;
            pipeRows[i / 3][i % 3] = centerPipes[i];
            pipeColumns[i % 3][i / 3] = centerPipes[i];
        }

        _moduleId = _moduleIdCounter++;
        Debug.LogFormat("[Lasers #{0}] Initializing...", _moduleId);
        Debug.LogFormat("[Lasers #{0}] Randomizing lasers...", _moduleId);
        pipeOrder.AddRange(Enumerable.Range(1, 9));
        bool temp = false;
        while (!temp) temp = PipeRandomization();
        r = 0;
        remember = new List<Renderer>();
        Debug.LogFormat("[Lasers #{0}] Laser order, from left to right: {1}", _moduleId, message);
        Debug.LogFormat("[Lasers #{0}] The laser numbers in the topmost row are {1}", _moduleId, numbers[0].text + ", " + numbers[1].text + ", and " + numbers[2].text);
        Debug.LogFormat("[Lasers #{0}] The digital root is {1}", _moduleId, rowRoot);
        Debug.LogFormat("[Lasers #{0}] Current laser color is red", _moduleId);
        Valid();

        for (int i = 0; i < pipes.Length; i++)
        {
            int j = i;
            pipes[i].OnInteract += delegate () { StartCoroutine(Selection(j)); return false; };
        }

        Module.OnActivate += delegate () { canPress = true; activated = true; };
    }

    bool PipeRandomization()
    {
        foreach (TextMesh text in numbers)
        {
            var i = UnityEngine.Random.Range(0, pipeOrder.Count);
            text.text = pipeOrder[i].ToString();
            pipeOrder.RemoveAt(i);
            message += text.text + ", ";
        }
        message = message.Remove(message.Length - 2, 1);
        if (Check()) return true;
        else return false;
    }

    bool Check()
    {
        for (int i = 0; i < pipes.Length; i++)
        {
            pipeOrder.Add(int.Parse(numbers[i].text));
        }
        originalTime = (int)Bomb.GetTime();
        timeRoot = (originalTime / 60) + 1;
        moduleParity = Bomb.GetModuleNames().Count() % 2;
        for (int i = 0; i < 3; i++) rowRoot += int.Parse(numbers[i].text);
        for (int i = 2; i < 9 && !(i % 3 == 0); i++) columnRoot += int.Parse(numbers[i].text);
        while (timeRoot > 9) timeRoot = timeRoot.ToString().ToCharArray().Sum(x => x - '0');
        while (rowRoot > 9) rowRoot = rowRoot.ToString().ToCharArray().Sum(x => x - '0');
        while (columnRoot > 9) columnRoot = columnRoot.ToString().ToCharArray().Sum(x => x - '0');
        var check = new[] { false, false, false, false, false, false, false };
        var orderCopy = new List<int>();
        var available = new List<int>();
        var hold = new List<List<int>>();
        orderCopy = new List<int>(pipeOrder);

        for (int i = 0; i < 7; i++)
        {
            available = new List<int>();
            foreach (int num in orderCopy)
            {
                var select = pipeOrder.IndexOf(num);
                var compare = 0;
                switch (i)
                {
                    case 0:
                        compare = pipeOrder.IndexOf(rowRoot);
                        if (Rules(i, select, compare))
                        {
                            available.Add(select + 1);
                        }
                        break;
                    case 1:
                        r = 1;
                        foreach (int num2 in hold[0])
                        {
                            var temp = num2 - 1;
                            if (remember.Count < 1) remember.Add(centerPipes[temp]);
                            else remember[0] = centerPipes[temp];
                            if (!select.Equals(temp) && !IsAdjacent(centerPipes[select]))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 2:
                        compare = pipeOrder.IndexOf(columnRoot);
                        foreach (int num2 in hold[1])
                        {
                            var num3 = (num2.ToString().Last() - '0') - 1;
                            var num4 = (num2.ToString().First() - '0') - 1;
                            if (!select.EqualsAny(num3, num4) && Rules(i, select, compare))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 3:
                        r = 3;
                        foreach (int num2 in hold[2])
                        {
                            var num3 = (num2.ToString().Last() - '0') - 1;
                            var num1 = num2.ToString().ToCharArray().Select(x => (x - '0') - 1).ToArray();
                            if (remember.Count < 3)
                            {
                                remember[0] = null;
                                remember.Add(null);
                                remember.Add(centerPipes[num3]);
                            }
                            else remember[2] = centerPipes[num3];
                            if (!num1.Contains(select) && IsAdjacent(centerPipes[select]))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 4:
                        remember[2] = null;
                        compare = pipeOrder.IndexOf(timeRoot);
                        foreach (int num2 in hold[3])
                        {
                            var num1 = num2.ToString().ToCharArray().Select(x => (x - '0') - 1).ToArray();
                            if (!num1.Contains(select) && Rules(i, select, compare))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 5:
                        foreach (int num2 in hold[4])
                        {
                            var num1 = num2.ToString().ToCharArray().Select(x =>( x - '0') - 1).ToArray();
                            if (!num1.Contains(select) && !(num % 2 == moduleParity))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 6:
                        r = 6;
                        foreach (int num2 in hold[5])
                        {
                            var num4 = (num2.ToString()[4] - '0') - 1;
                            var num1 = num2.ToString().ToCharArray().Select(x => (x - '0') - 1).ToArray();
                            if (remember.Count < 5)
                            {
                                remember.Add(null);
                                remember.Add(centerPipes[num4]);
                            }
                            else remember[4] = centerPipes[num4];
                            if (!num1.Contains(select) && !IsAdjacent(centerPipes[select]))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                }
            }
            hold.Add(new List<int>(available));
            if (hold[i].Count > 0) check[i] = true;
        }
        combination = new List<int>(hold[6]);
        if (check.Contains(false)) return false;
        else return true;
    }

    bool Rules(int i, int select, int compare)
    {
        switch (i)
        {
            case 0:
                if (!pipeRows.Select(x => x.Contains(centerPipes[select]) && x.Contains(centerPipes[compare])).Contains(true)) return true;
                break;
            case 1:
                if (!remember.Contains(centerPipes[select]) && !IsAdjacent(centerPipes[select])) return true;
                break;
            case 2:
                if (!remember.Contains(centerPipes[select]) && !pipeColumns.Select(x => x.Contains(centerPipes[select]) && x.Contains(centerPipes[compare])).Contains(true)) return true;
                break;
            case 3:
                if (!remember.Contains(centerPipes[select]) && IsAdjacent(centerPipes[select])) return true;
                break;
            case 4:
                if (!remember.Contains(centerPipes[select]) && !pipeRows.Concat(pipeColumns).Select(x => x.Contains(centerPipes[select]) && x.Contains(centerPipes[compare])).Contains(true)) return true;
                break;
            case 5:
                if (!remember.Contains(centerPipes[select]) && !(pipeOrder[select] % 2 == moduleParity)) return true;
                break;
            case 6:
                if (!remember.Contains(centerPipes[select]) && !IsAdjacent(centerPipes[select])) return true;
                break;
        }
        return false;
    }

    void Valid()
    {
        var available = new List<int>();
        foreach (int num in combination)
        {
            var num2 = num.ToString();
            var num3 = num2[r] - '0' - 1;
            if (remember.Count() > 0)
            {
                var num4 = remember.Select(x => (x.name[4] - '0')).ToArray();
                var num5 = num2.ToCharArray().Select(x => x - '0').ToArray();
                var num6 = new List<bool>();
                for (int j = 0; j < num4.Count(); j++)
                {
                    if (num4[j].Equals(num5[j])) num6.Add(true);
                    else num6.Add(false);
                }
                if (!available.Contains(num3) && !remember.Contains(centerPipes[num3]) && !num6.Contains(false)) available.Add(num3);
            }
            else
            {
                if (!available.Contains(num3)) available.Add(num3);
            }
        }
        if (available.Count < 1)
        {
            Debug.LogFormat("[Lasers #{0}] No acceptable selections available. A Strike is necessary.", _moduleId);
            special = true;
            return;
        }
        message = String.Join(", ", available.Select(x => numbers[x].text).ToArray());
        Debug.LogFormat("[Lasers #{0}] Acceptable selections are {1}", _moduleId, message);
    }

    IEnumerator Selection(int i)
    {
        if (!activated) yield break;
        while (!canPress) yield return null;
        isStriking = false;
        var a = Connections.Where(x => x.name.Contains((i + 1).ToString()));
        var b = a.Concat(new[] { centerPipes[i] });
        var c = Connections.Where(x => !x.name.Contains((i + 1).ToString()) && x.gameObject.activeSelf);
        var d = c.Concat(centerPipes.Where(x => !x.Equals(centerPipes[i]) && !remember.Contains(x)));
        var e = Fade(Connections.Concat(centerPipes).ToArray(), Connections.Concat(centerPipes).ToArray().Count(), 0, 1.5f, Color.red, Color.black);
        canPress = false;
        switch (r)
        {
            case 0:
                if (Rules(r, i, pipeOrder.IndexOf(rowRoot)))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1.5f, Color.red, Color.black, Color.red, orange);
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }
                    Debug.LogFormat("[Lasers #{0}] The previously selected laser was the {1} laser", _moduleId, PipePosition(i));
                    Debug.LogFormat("[Lasers #{0}] Current laser color is orange", _moduleId);
                    Valid();
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, Color.red, new Color(0.5f, 0, 0, 0.5f), Color.red, new Color(1, 70 / 255f, 0, 0.5f));
                    goto case 9;
                }
                break;
            case 1:
                if (Rules(r, i, 0))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1.5f, orange, Color.black, orange, Color.yellow);
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }
                    Debug.LogFormat("[Lasers #{0}] The laser numbers in the two right columns, from top to bottom, are {1}", _moduleId, numbers[1].text + ", " + numbers[4].text + ", " + numbers[7].text + ", " + numbers[2].text + ", " + numbers[5].text + ", and " + numbers[8].text);
                    Debug.LogFormat("[Lasers #{0}] The digital root is {1}", _moduleId, columnRoot);
                    Debug.LogFormat("[Lasers #{0}] Current laser color is yellow", _moduleId);
                    Valid();
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, orange, new Color(0.5f, 70 / 255f, 0, 0.5f), orange, new Color(1f, 0.74f, 0.008f, 0.5f));
                    goto case 9;
                }
                break;
            case 2:
                if (Rules(r, i, pipeOrder.IndexOf(columnRoot)))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1.5f, Color.yellow, Color.black, Color.yellow, Color.green);
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }
                    Debug.LogFormat("[Lasers #{0}] The previously selected laser was the {1} laser", _moduleId, PipePosition(i));
                    Debug.LogFormat("[Lasers #{0}] Current laser color is green", _moduleId);
                    Valid();
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, Color.yellow, new Color(0.5f, 0.46f, 0.008f, 0.5f), Color.yellow, new Color(0.5f, 0.96f, 0.008f, 0.5f));
                    goto case 9;
                }
                break;
            case 3:
                if (Rules(r, i, 0))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1.5f, Color.green, Color.black, Color.green, Color.blue);
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }
                    Debug.LogFormat("[Lasers #{0}] The starting time was {1} {2}", _moduleId, originalTime / 60, originalTime / 60 == 1 ? "minute" : "minutes" );
                    Debug.LogFormat("[Lasers #{0}] The digital root is {1}", _moduleId, timeRoot);
                    Debug.LogFormat("[Lasers #{0}] Current laser color is blue", _moduleId);
                    Valid();
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, Color.green, new Color(0, 0.5f, 0, 0.5f), Color.green, new Color(0, 0.5f, 0.5f, 0.5f));
                    goto case 9;
                }
                break;
            case 4:
                if (Rules(r, i, pipeOrder.IndexOf(timeRoot)))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1.5f, Color.blue, Color.black, Color.blue, purple);
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }
                    Debug.LogFormat("[Lasers #{0}] There {1} {2} {3} on the bomb", _moduleId, Bomb.GetModuleNames().Count == 1 ? "is" : "are", Bomb.GetModuleNames().Count, Bomb.GetModuleNames().Count == 1 ? "module" : "modules");
                    Debug.LogFormat("[Lasers #{0}] The parity is {1}", _moduleId, moduleParity == 0 ? "even" : "odd");
                    Debug.LogFormat("[Lasers #{0}] Current laser color is purple", _moduleId);
                    Valid();
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, Color.blue, new Color(0, 0, 0.5f, 0.5f), Color.blue, new Color(0.25f, 0, 0.75f, 0.5f));
                    goto case 9;
                }
                break;
            case 5:
                if (Rules(r, i, 0))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1.5f, purple, Color.black, purple, Color.white);
                    while (e.MoveNext())
                    {
                        yield return e.Current;
                    }
                    var blue = Array.IndexOf(centerPipes, remember[4]);
                    Debug.LogFormat("[Lasers #{0}] The laser selected in the blue stage was the {1} laser", _moduleId, PipePosition(blue));
                    Debug.LogFormat("[Lasers #{0}] Current laser color is white", _moduleId);
                    Valid();
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, purple, new Color(0.25f, 0, 0.25f, 0.5f), purple, new Color(0.75f, 0.5f, 0.75f, 0.5f));
                    goto case 9;
                }
                break;
            case 6:
                if (Rules(r, i, 0))
                {
                    r++;
                    remember.Add(centerPipes[i]);
                    e = Fade(Connections.Concat(centerPipes).ToArray(), Connections.Concat(centerPipes).Count(), 0, 1.5f, Color.white, Color.black);
                    while (e.MoveNext()) yield return e.Current;
                    StartCoroutine(Finale());
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, Color.white, new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, new Color(0.75f, 0.75f, 0.75f, 0.5f));
                    goto case 9;
                }
                break;
            case 7:
                Debug.LogFormat("[Lasers #{0}] Module completed", _moduleId);
                goto end;
            case 9:
                while (e.MoveNext())
                {
                    yield return e.Current;
                }
                Module.HandleStrike();
                Restart(i, b.Concat(d).ToList(), b.ToList()[0].material.color, d.ToList()[0].material.color);
                r = 0;
                remember = new List<Renderer>();
                break;
        }
        PipeCheck();
        canPress = true;
        end: yield break;
    }

    void Restart(int i, List<Renderer> a, Color colorFade, Color colorIntent)
    {
        isStriking = true;
        var b = a.Where(x => x.material.color.Equals(colorFade) && !startOff.Contains(x)).ToList();
        var c = a.Where(x => x.material.color.Equals(colorIntent) && !startOff.Contains(x)).ToList();
        var d = Connections.Where(x => !startOff.Contains(x)).Concat(remember);
        var e = Fade(d.ToArray(), d.Count(), 0, 0.5f, Color.black, Color.red);
        StartCoroutine(e);
        e = Fade(b.ToArray(), b.Count(), 0, 0.5f, colorFade, Color.red);
        StartCoroutine(e);
        e = Fade(c.ToArray(), c.Count(), 0, 0.5f, colorIntent, Color.red);
        StartCoroutine(e);
        if (!special) Debug.LogFormat("[Lasers #{0}] Invalid laser selected", _moduleId);
        else Debug.LogFormat("[Lasers #{0}] Laser selected, module has been reset", _moduleId);
        Debug.LogFormat("[Lasers #{0}] Current laser color is red", _moduleId);
        special = false;
    }

    void PipeCheck()
    {
        foreach (Renderer ren in centerPipes)
        {
            var a = ren.material.color;
            if (ren.gameObject.activeSelf && !Connections.Where(x => x.name.Contains(ren.name[4].ToString())).Select(x => x.gameObject.activeSelf).Contains(true) && !ren.Equals(centerPipes[4]))
            {
                switch (ren.name[4] - '0')
                {
                    case 1:
                        Connections[20].gameObject.SetActive(true);
                        Connections[22].gameObject.SetActive(true);
                        Connections[20].material.color = a;
                        Connections[22].material.color = a;
                        break;
                    case 2:
                        Connections[30].gameObject.SetActive(true);
                        Connections[30].material.color = a;
                        break;
                    case 3:
                        Connections[21].gameObject.SetActive(true);
                        Connections[36].gameObject.SetActive(true);
                        Connections[21].material.color = a;
                        Connections[36].material.color = a;
                        break;
                    case 4:
                        Connections[44].gameObject.SetActive(true);
                        Connections[44].material.color = a;
                        break;
                    case 6:
                        Connections[45].gameObject.SetActive(true);
                        Connections[45].material.color = a;
                        break;
                    case 7:
                        Connections[23].gameObject.SetActive(true);
                        Connections[50].gameObject.SetActive(true);
                        Connections[23].material.color = a;
                        Connections[50].material.color = a;
                        break;
                    case 8:
                        Connections[31].gameObject.SetActive(true);
                        Connections[31].material.color = a;
                        break;
                    case 9:
                        Connections[37].gameObject.SetActive(true);
                        Connections[51].gameObject.SetActive(true);
                        Connections[37].material.color = a;
                        Connections[51].material.color = a;
                        break;
                }
            }
        }
    }

    bool IsAdjacent(Renderer check)
    {
        if (r.Equals(0)) return false;
        var copy = remember[0];
        if (r.Equals(3)) copy = remember[2];
        if (r.Equals(6)) copy = remember[4];
        switch (check.name)
        {
            case "Pipe1":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[1], centerPipes[3])) return true;
                if (r.EqualsAny(3, 6) && copy.Equals(centerPipes[4])) return true;
                break;
            case "Pipe2":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[0], centerPipes[2], centerPipes[4])) return true;
                if (r.EqualsAny(3, 6) && copy.EqualsAny(centerPipes[3], centerPipes[5])) return true;
                break;
            case "Pipe3":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[1], centerPipes[5])) return true;
                if (r.EqualsAny(3, 6) && copy.Equals(centerPipes[4])) return true;
                break;
            case "Pipe4":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[0], centerPipes[4], centerPipes[6])) return true;
                if (r.EqualsAny(3, 6) && copy.EqualsAny(centerPipes[1], centerPipes[7])) return true;
                break;
            case "Pipe5":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[1], centerPipes[3], centerPipes[5], centerPipes[7])) return true;
                if (r.EqualsAny(3, 6) && copy.EqualsAny(centerPipes[0], centerPipes[2], centerPipes[6], centerPipes[8])) return true;
                break;
            case "Pipe6":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[2], centerPipes[4], centerPipes[8])) return true;
                if (r.EqualsAny(3, 6) && copy.EqualsAny(centerPipes[1], centerPipes[7])) return true;
                break;
            case "Pipe7":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[3], centerPipes[7])) return true;
                if (r.EqualsAny(3, 6) && copy.Equals(centerPipes[4])) return true;
                break;
            case "Pipe8":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[6], centerPipes[4], centerPipes[8])) return true;
                if (r.EqualsAny(3, 6) && copy.EqualsAny(centerPipes[3], centerPipes[5])) return true;
                break;
            case "Pipe9":
                if (r.EqualsAny(1, 6) && copy.EqualsAny(centerPipes[5], centerPipes[7])) return true;
                if (r.EqualsAny(3, 6) && copy.Equals(centerPipes[4])) return true;
                break;
        }
        return false;
    }

    string PipePosition(int position)
    {
        switch (position)
        {
            case 0:
                message = "top left";
                break;
            case 1:
                message = "top middle";
                break;
            case 2:
                message = "top right";
                break;
            case 3:
                message = "middle left";
                break;
            case 4:
                message = "middle";
                break;
            case 5:
                message = "middle right";
                break;
            case 6:
                message = "bottom left";
                break;
            case 7:
                message = "bottom middle";
                break;
            case 8:
                message = "bottom right";
                break;
        }
        return message;
    }

    IEnumerator Fade(Renderer[] rendererList, int rLC1, int rLC2, float time, Color colorOriginal1, Color colorFinal1, Color? colorOriginal2 = null, Color? colorFinal2 = null)
    {
        var colorOriginalT = colorOriginal2 ?? Color.black;
        var colorFinalT = colorFinal2 ?? Color.black;
        for (int i = 0; i < rendererList.Count(); i++)
        {
            if ((colorOriginal1.Equals(Color.black) && i < rLC1) || (colorOriginal2.Equals(Color.black) && rLC2 > 0 && i >= rLC1) ) rendererList[i].gameObject.SetActive(true);
        }
        for (float t = 0.0f; t < time; t += Time.deltaTime)
        {
            for (int i = 0; i < rLC1; i++)
            {
                rendererList[i].material.color = Color.Lerp(colorOriginal1, colorFinal1, t / time);
            }
            yield return null;
            if (rLC2 > 0)
            {
                for (int i = rLC1; i < rendererList.Count(); i++)
                {
                    rendererList[i].material.color = Color.Lerp(colorOriginalT, colorFinalT, t / time);
                }
            }
        }
        for (int i = 0; i < rendererList.Count(); i++)
        {
            if ((colorFinal1.Equals(Color.black) && i < rLC1) || (colorFinal2.Equals(Color.black) && rLC2 > 0 && i >= rLC1)) rendererList[i].gameObject.SetActive(false);
        }
    }

    IEnumerator Finale()
    {
        for (int i = 0; i < remember.Count; i++)
        {
            var fade = Fade(new[] { remember[i] }, 1, 0, 0.5f, Color.black, colorList[i]);
            while (fade.MoveNext()) yield return fade.Current;
            if (i < remember.Count - 1)
            {
                var a = Connections.Where(x => x.name.Contains(remember[i].name[4]) && x.name.Contains(remember[i + 1].name[4])).ToList();
                fade = Fade(a.ToArray(), a.Count, 0, 0.5f, Color.black, colorList[i]);
                while (fade.MoveNext()) yield return fade.Current;
            }
        }

        Module.HandlePass();
    }

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var commands = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var commands2 = command.ToLowerInvariant().Replace(" ", "").Replace("press", "");
        var n = 0;

        if (commands.Count() < 2 || commands.Count() > 8) yield break;
        else if (!commands[0].Equals("press")) yield break;
        else if (commands2.Length > 8 || !int.TryParse(commands2, out n)) yield break;

        foreach (char c in commands2)
        {
            n = c - '0' - 1;
            yield return null;
            if (remember.Contains(centerPipes[n])) yield break;
            yield return new KMSelectable[] { pipes[n] };
            yield return null;
            yield return new WaitUntil(() => canPress);
            if (isStriking) StopAllCoroutines();
        }
    }
}
