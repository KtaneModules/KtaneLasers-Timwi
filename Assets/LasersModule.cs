using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;
using Lasers;

using Rnd = UnityEngine.Random;

public class LasersModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] Hatches;
    public Transform[] Lasers;
    private bool[] _isLaserUp;
    public Texture[] Textures;
    private Texture[] _leftTextures, _rightTextures;
    public GameObject HatchesParent;
    private MeshRenderer[] _leftHatches, _rightHatches;

    static Color orange = new Color(1f, 140 / 255f, 0), purple = new Color(0.5f, 0, 0.5f);
    private readonly List<Color> colorList = new List<Color> { Color.red, orange, Color.yellow, Color.green, Color.blue, purple, Color.white };

    private List<int> laserOrder = new List<int>();
    private List<int> hatchesAlreadyPressed = new List<int>();
    private int _stage;
    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private int rowRoot, columnRoot, timeRoot, moduleParity;
    private Queue<IEnumerable> queue = new Queue<IEnumerable>();
    private bool _animating;

    private static readonly string[] _stageNames = new[] { "red", "orange", "yellow", "green", "blue", "purple", "white" };
    private static readonly string[] _positionNames = new[] { "top left", "top middle", "top right", "middle left", "middle center", "middle right", "bottom left", "bottom middle", "bottom right" };

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        _leftTextures = new Texture[9];
        _rightTextures = new Texture[9];
        _leftHatches = new MeshRenderer[9];
        _rightHatches = new MeshRenderer[9];
        _isLaserUp = new bool[9];

        for (int i = 0; i < 9; i++)
        {
            _leftTextures[i] = Textures.First(t => t.name == "Hatch-" + (i + 1) + "-left");
            _rightTextures[i] = Textures.First(t => t.name == "Hatch-" + (i + 1) + "-right");
            _leftHatches[i] = HatchesParent.transform.Find("Hatch" + (i + 1)).Find("Left").GetComponent<MeshRenderer>();
            _rightHatches[i] = HatchesParent.transform.Find("Hatch" + (i + 1)).Find("Right").GetComponent<MeshRenderer>();
        }

        timeRoot = (int) Bomb.GetTime() % 9 + 1;    // Note: the rule is “time in minutes plus one”; the +1 cancels with a −1 in the formula for digital root
        moduleParity = Bomb.GetModuleNames().Count() % 2;

        // Randomize the order of the lasers.
        // I have independently verified that every possible permutation has a solution for every possible timer digital root and module number parity.
        laserOrder.AddRange(Enumerable.Range(1, 9));
        laserOrder.Shuffle();

        rowRoot = (laserOrder[0] + laserOrder[1] + laserOrder[2] - 1) % 9 + 1;
        columnRoot = (new[] { 1, 2, 4, 5, 7, 8 }.Sum(x => laserOrder[x]) - 1) % 9 + 1;

        Debug.LogFormat("[Lasers #{0}] Laser numbers in reading order: {1}", _moduleId, laserOrder.JoinString(", "));
        Debug.LogFormat("[Lasers #{0}] The laser numbers in the topmost row have digital root = {1}.", _moduleId, rowRoot);
        Debug.LogFormat("[Lasers #{0}] The laser numbers in the rightmost two columns have digital root = {1}.", _moduleId, columnRoot);
        Debug.LogFormat("[Lasers #{0}] The time in minutes plus one has digital root = {1}.", _moduleId, timeRoot);
        Debug.LogFormat("[Lasers #{0}] The number of modules on the bomb has parity = {1}.", _moduleId, moduleParity);

        for (int i = 0; i < 9; i++)
        {
            _leftHatches[i].material.mainTexture = _leftTextures[laserOrder[i] - 1];
            _rightHatches[i].material.mainTexture = _rightTextures[laserOrder[i] - 1];
            Hatches[i].OnInteract = GetHatchPressHandler(i);
        }

        StartCoroutine(ProcessQueue());
        LogPermissibleLasers();
    }

    public GameObject Sphere;
    //private void Update()
    //{
    //    var spherePlane = new Plane(transform.TransformVector(Vector3.up), transform.TransformVector(Sphere.transform.localPosition));
    //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    //    float distance = 0;
    //    if (spherePlane.Raycast(ray, out distance))
    //    {
    //        var pt = ray.GetPoint(distance);
    //        Sphere.transform.position = pt;
    //    }
    //}

    bool IsValid(int hatch)
    {
        switch (_stage)
        {
            case 0:
                return hatch / 3 != laserOrder.IndexOf(rowRoot) / 3;
            case 1:
                return !hatchesAlreadyPressed.Contains(hatch) && (
                    hatch % 3 == hatchesAlreadyPressed[0] % 3 ? Math.Abs(hatch / 3 - hatchesAlreadyPressed[0] / 3) != 1 :
                    Math.Abs(hatch % 3 - hatchesAlreadyPressed[0] % 3) == 1 ? hatch / 3 != hatchesAlreadyPressed[0] / 3 : true);
            case 2:
                return !hatchesAlreadyPressed.Contains(hatch) && hatch % 3 != laserOrder.IndexOf(columnRoot) % 3;
            case 3:
                return !hatchesAlreadyPressed.Contains(hatch) && Math.Abs(hatch % 3 - hatchesAlreadyPressed[2] % 3) == 1 && Math.Abs(hatch / 3 - hatchesAlreadyPressed[2] / 3) == 1;
            case 4:
                return !hatchesAlreadyPressed.Contains(hatch) && hatch % 3 != laserOrder.IndexOf(timeRoot) % 3 && hatch / 3 != laserOrder.IndexOf(timeRoot) / 3;
            case 5:
                return !hatchesAlreadyPressed.Contains(hatch) && laserOrder[hatch] % 2 != moduleParity;
            case 6:
                return !hatchesAlreadyPressed.Contains(hatch) && (Math.Abs(hatch % 3 - hatchesAlreadyPressed[4] % 3) > 1 || Math.Abs(hatch / 3 - hatchesAlreadyPressed[4] / 3) > 1);
        }
        return false;
    }

    IEnumerator ProcessQueue()
    {
        while (true)
        {
            while (queue.Count > 0)
            {
                IEnumerable items = queue.Dequeue();
                foreach (var item in items)
                    yield return item;
            }
            yield return null;
        }
    }

    KMSelectable.OnInteractHandler GetHatchPressHandler(int i)
    {
        return delegate
        {
            if (IsValid(i))
            {
                Debug.LogFormat("[Lasers #{0}] For stage {1}, you pressed Laser {2} ({3}) — acceptable.", _moduleId, _stageNames[_stage], laserOrder[i], _positionNames[i]);
                _stage++;
                moveLasersUpDown(true, i);
                if (_stage == 7)
                {
                    Debug.LogFormat("[Lasers #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    hatchesAlreadyPressed = null;
                }
                else
                {
                    hatchesAlreadyPressed.Add(i);
                    LogPermissibleLasers();
                }
            }
            else
            {
                Debug.LogFormat("[Lasers #{0}] For stage {1}, you pressed Laser {2} ({3}) — forbidden! Strike and module reset.", _moduleId, _stageNames[_stage], laserOrder[i], _positionNames[i]);
                Module.HandleStrike();
                Restart();
            }
            return false;
        };
    }

    private void moveLasersUpDown(bool up, params int[] ixs)
    {
        queue.Enqueue(moveLasersUpDownAnimation(up, ixs));
    }

    private IEnumerator _openCloseHatch(int i, bool open, Action whenDone = null)
    {
        const float duration = 1f;
        var elapsed = 0f;

        if (!open)
            yield return new WaitForSeconds(1f);

        var leftOpen = new Vector3(-0.0201f, 0, 0);
        var leftClosed = new Vector3(-0.0001f, 0, 0);
        var rightOpen = new Vector3(0.0201f, 0, 0);
        var rightClosed = new Vector3(0.0001f, 0, 0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _leftHatches[i].transform.localPosition = Vector3.Lerp(open ? leftClosed : leftOpen, open ? leftOpen : leftClosed, elapsed / duration);
            _leftHatches[i].transform.localRotation = Quaternion.Slerp(open ? Quaternion.identity : Quaternion.Euler(0, 0, 90), open ? Quaternion.Euler(0, 0, 90) : Quaternion.identity, elapsed / duration);
            _rightHatches[i].transform.localPosition = Vector3.Lerp(open ? rightClosed : rightOpen, open ? rightOpen : rightClosed, elapsed / duration);
            _rightHatches[i].transform.localRotation = Quaternion.Slerp(open ? Quaternion.identity : Quaternion.Euler(0, 0, -90), open ? Quaternion.Euler(0, 0, -90) : Quaternion.identity, elapsed / duration);
            yield return null;
        }

        if (whenDone != null)
            whenDone();
    }

    private IEnumerator _moveLaser(int i, bool up, Action whenDone = null)
    {
        var upPos = new Vector3(0, 0, .02f);
        var downPos = new Vector3(0, -.05f, .02f);

        if (Lasers[i] == null)
        {
            Lasers[i] = Instantiate(Lasers[0]);
            Lasers[i].parent = Hatches[i].transform;
            Lasers[i].localPosition = up ? downPos : upPos;
            Lasers[i].localRotation = Quaternion.Euler(0, Rnd.Range(0f, 360f), 0);
            Lasers[i].localScale = Lasers[0].localScale;
            Lasers[i].name = "Laser";
        }

        if (up)
            yield return new WaitForSeconds(.5f);
        const float duration = 1.5f;
        var elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Lasers[i].transform.localPosition = Vector3.Lerp(up ? downPos : upPos, up ? upPos : downPos, elapsed / duration);
            yield return null;
        }

        if (whenDone != null)
            whenDone();
    }

    private IEnumerable moveLasersUpDownAnimation(bool up, params int[] ixs)
    {
        _animating = true;
        var unfinished = 2 * ixs.Length;
        for (int i = 0; i < ixs.Length; i++)
        {
            _isLaserUp[ixs[i]] = up;
            StartCoroutine(_openCloseHatch(ixs[i], up, whenDone: () => { unfinished--; }));
            StartCoroutine(_moveLaser(ixs[i], up, whenDone: () => { unfinished--; }));
        }
        yield return new WaitUntil(() => unfinished == 0);
        _animating = false;
    }

    void Restart()
    {
        _stage = 0;
        hatchesAlreadyPressed.Clear();
        queue.Clear();
        moveLasersUpDown(false, Enumerable.Range(0, 9).Where(ix => _isLaserUp[ix]).ToArray());
        LogPermissibleLasers();
    }

    private void LogPermissibleLasers()
    {
        Debug.LogFormat("[Lasers #{0}] Stage {1}: Permissible lasers: {2}", _moduleId, _stageNames[_stage], Enumerable.Range(0, 9).Where(hatch => IsValid(hatch)).Select(hatch => laserOrder[hatch]).JoinString(", "));
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "Select a hatch in position 1–9 using “!{0} press 4”. Hatch positions are 1–9 in reading order. You may select multiple hatches by using “!{0} press 1235679”.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var commands = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var commands2 = command.ToLowerInvariant().Replace(" ", "").Replace("press", "");

        int n;
        if (commands.Length < 2 || commands.Length > 8 || !commands[0].Equals("press", StringComparison.InvariantCultureIgnoreCase) || commands2.Length > 8 || !int.TryParse(commands2, out n))
            yield break;

        var buttons = new List<KMSelectable>();

        foreach (char c in commands2)
        {
            n = c - '0' - 1;
            if (hatchesAlreadyPressed.Contains(n))
                yield break;
            buttons.Add(Hatches[n]);
        }

        yield return null;

        foreach (var button in buttons)
        {
            button.OnInteract();
            yield return new WaitUntil(() => !_animating);
        }
    }
}
