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
    public Texture[] Textures;
    public GameObject HatchesParent;

    static Color orange = new Color(1f, 155 / 255f, 0), purple = new Color(0.5f, 0, 0.5f);
    private readonly List<Color> colorList = new List<Color> { Color.red, orange, Color.yellow, Color.green, Color.blue, purple, Color.white };

    private readonly Texture[] _leftTextures = new Texture[9];
    private readonly Texture[] _rightTextures = new Texture[9];
    private readonly MeshRenderer[] _leftHatches = new MeshRenderer[9];
    private readonly MeshRenderer[] _rightHatches = new MeshRenderer[9];
    private readonly Transform[] _laserLenses = new Transform[9];
    private readonly Transform[] _laserBeams = new Transform[9];
    private readonly MeshRenderer[] _laserBodies = new MeshRenderer[9];
    private readonly Quaternion[] _laserTargetRotations = new Quaternion[9];
    private readonly bool[] _isLaserUp = new bool[9];
    private List<int> _laserOrder = new List<int>();
    private List<int> _hatchesAlreadyPressed = new List<int>();
    private int _stage, _rowRoot, _columnRoot, _timeRoot, _moduleParity;
    private Queue<IEnumerable> queue = new Queue<IEnumerable>();
    private bool _animating;
    private bool _isSolved;
    private int? _mouseOnHatch;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private static readonly string[] _stageNames = new[] { "red", "orange", "yellow", "green", "blue", "purple", "white" };
    private static readonly string[] _positionNames = new[] { "top left", "top middle", "top right", "middle left", "middle center", "middle right", "bottom left", "bottom middle", "bottom right" };

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        _laserLenses[0] = Lasers[0].Find("Lens");
        _laserBeams[0] = Lasers[0].Find("Beam");
        _laserBodies[0] = Lasers[0].Find("Body").GetComponent<MeshRenderer>();

        for (int i = 0; i < 9; i++)
        {
            _leftTextures[i] = Textures.First(t => t.name == "Hatch-" + (i + 1) + "-left");
            _rightTextures[i] = Textures.First(t => t.name == "Hatch-" + (i + 1) + "-right");
            _leftHatches[i] = HatchesParent.transform.Find("Hatch" + (i + 1)).Find("Left").GetComponent<MeshRenderer>();
            _rightHatches[i] = HatchesParent.transform.Find("Hatch" + (i + 1)).Find("Right").GetComponent<MeshRenderer>();
        }

        _timeRoot = (int) Bomb.GetTime() % 9 + 1;    // Note: the rule is “time in minutes plus one”; the +1 cancels with a −1 in the formula for digital root
        _moduleParity = Bomb.GetModuleNames().Count() % 2;

        // Randomize the order of the lasers.
        // I have independently verified that every possible permutation has a solution for every possible timer digital root and module number parity.
        _laserOrder.AddRange(Enumerable.Range(1, 9));
        _laserOrder.Shuffle();

        _rowRoot = (_laserOrder[0] + _laserOrder[1] + _laserOrder[2] - 1) % 9 + 1;
        _columnRoot = (new[] { 1, 2, 4, 5, 7, 8 }.Sum(x => _laserOrder[x]) - 1) % 9 + 1;

        Debug.LogFormat("[Lasers #{0}] Laser numbers in reading order: {1}", _moduleId, _laserOrder.JoinString(", "));
        Debug.LogFormat("[Lasers #{0}] The laser numbers in the topmost row have digital root = {1}.", _moduleId, _rowRoot);
        Debug.LogFormat("[Lasers #{0}] The laser numbers in the rightmost two columns have digital root = {1}.", _moduleId, _columnRoot);
        Debug.LogFormat("[Lasers #{0}] The time in minutes plus one has digital root = {1}.", _moduleId, _timeRoot);
        Debug.LogFormat("[Lasers #{0}] The number of modules on the bomb has parity = {1}.", _moduleId, _moduleParity);

        for (int i = 0; i < 9; i++)
        {
            _leftHatches[i].material.mainTexture = _leftTextures[_laserOrder[i] - 1];
            _rightHatches[i].material.mainTexture = _rightTextures[_laserOrder[i] - 1];
            Hatches[i].OnInteract = GetHatchPressHandler(i);
            Hatches[i].OnDeselect = GetMouseSetter(null);
            Hatches[i].OnSelect = GetMouseSetter(i);
        }

        StartCoroutine(ProcessQueue());
        LogPermissibleLasers();
    }

    private Action GetMouseSetter(int? val)
    {
        return delegate
        {
            _mouseOnHatch = val;
        };
    }

    private void Update()
    {
        for (int i = 0; i < _hatchesAlreadyPressed.Count; i++)
        {
            var ix = _hatchesAlreadyPressed[i];
            var laser = Lasers[ix];
            var lens = _laserLenses[ix];
            var beam = _laserBeams[ix];
            if (lens == null || beam == null)
                continue;

            Vector3 targetPoint;
            float distance;
            RaycastHit hit;

            if (i < _hatchesAlreadyPressed.Count - 1)
                targetPoint = transform.InverseTransformPoint(Hatches[_hatchesAlreadyPressed[i + 1]].transform.TransformPoint(0, 0, .02f));
            else if (i == 6 && _isSolved)
                targetPoint = transform.InverseTransformPoint(Hatches[_hatchesAlreadyPressed[0]].transform.TransformPoint(0, 0, .02f));
            else if (_mouseOnHatch != null && _mouseOnHatch != ix)
                targetPoint = transform.InverseTransformPoint(Hatches[_mouseOnHatch.Value].transform.TransformPoint(0, 0, .02f));
            else if (!Input.mousePresent)
                goto skipRotation;
            else
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (!new Plane(transform.TransformVector(Vector3.up), lens.TransformPoint(0, 0, 0)).Raycast(ray, out distance))
                    goto skipRotation;
                var pt = ray.GetPoint(distance);
                targetPoint = transform.InverseTransformPoint(pt);
            }
            var laserPosition = transform.InverseTransformPoint(laser.transform.TransformPoint(0, 0, 0));
            var targetRotation = Quaternion.Euler(0, -Mathf.Atan2(targetPoint.z - laserPosition.z, targetPoint.x - laserPosition.x) * 180 / Mathf.PI, 0);
            laser.localRotation = Quaternion.RotateTowards(laser.localRotation, targetRotation, 30 * Time.deltaTime);

            skipRotation:
            var localDistance = .1f;
            if (Physics.Raycast(new Ray(lens.TransformPoint(0, 1.1f, 0), lens.TransformDirection(Vector3.up)), out hit))
                localDistance = laser.InverseTransformVector(0, hit.distance, 0).magnitude;
            beam.localPosition = new Vector3(localDistance / 2 + .016f, .031f, 0);
            beam.localScale = new Vector3(.0025f, localDistance / 2 + .001f, .0025f);
        }
    }

    bool IsValid(int hatch)
    {
        switch (_stage)
        {
            case 0:
                return hatch / 3 != _laserOrder.IndexOf(_rowRoot) / 3;
            case 1:
                return !_hatchesAlreadyPressed.Contains(hatch) && (
                    hatch % 3 == _hatchesAlreadyPressed[0] % 3 ? Math.Abs(hatch / 3 - _hatchesAlreadyPressed[0] / 3) != 1 :
                    Math.Abs(hatch % 3 - _hatchesAlreadyPressed[0] % 3) == 1 ? hatch / 3 != _hatchesAlreadyPressed[0] / 3 : true);
            case 2:
                return !_hatchesAlreadyPressed.Contains(hatch) && hatch % 3 != _laserOrder.IndexOf(_columnRoot) % 3;
            case 3:
                return !_hatchesAlreadyPressed.Contains(hatch) && Math.Abs(hatch % 3 - _hatchesAlreadyPressed[2] % 3) == 1 && Math.Abs(hatch / 3 - _hatchesAlreadyPressed[2] / 3) == 1;
            case 4:
                return !_hatchesAlreadyPressed.Contains(hatch) && hatch % 3 != _laserOrder.IndexOf(_timeRoot) % 3 && hatch / 3 != _laserOrder.IndexOf(_timeRoot) / 3;
            case 5:
                return !_hatchesAlreadyPressed.Contains(hatch) && _laserOrder[hatch] % 2 != _moduleParity;
            case 6:
                return !_hatchesAlreadyPressed.Contains(hatch) && (Math.Abs(hatch % 3 - _hatchesAlreadyPressed[4] % 3) > 1 || Math.Abs(hatch / 3 - _hatchesAlreadyPressed[4] / 3) > 1);
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
            Hatches[i].AddInteractionPunch(.2f);

            if (_isSolved)
                return false;

            if (IsValid(i))
            {
                Debug.LogFormat("[Lasers #{0}] For stage {1}, you pressed Laser {2} ({3}) — acceptable.", _moduleId, _stageNames[_stage], _laserOrder[i], _positionNames[i]);
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, Hatches[i].transform);
                moveLasersUpDown(true, colorList[_stage], i);
                _hatchesAlreadyPressed.Add(i);
                _stage++;
                if (_stage == 7)
                {
                    Debug.LogFormat("[Lasers #{0}] Module solved.", _moduleId);
                    Module.HandlePass();
                    _isSolved = true;
                }
                else
                    LogPermissibleLasers();
            }
            else
            {
                Debug.LogFormat("[Lasers #{0}] For stage {1}, you pressed Laser {2} ({3}) — forbidden! Strike and module reset.", _moduleId, _stageNames[_stage], _laserOrder[i], _positionNames[i]);
                Module.HandleStrike();
                Restart();
            }
            return false;
        };
    }

    private void moveLasersUpDown(bool up, Color color, params int[] ixs)
    {
        queue.Enqueue(moveLasersUpDownAnimation(up, color, ixs));
    }

    private IEnumerator _openCloseHatch(int i, bool open, Action whenDone = null)
    {
        const float duration = 1f;
        var elapsed = 0f;

        if (!open)
            yield return new WaitForSeconds(1.5f);

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

        Lasers[i].localPosition = up ? downPos : upPos;

        if (up)
        {
            _laserBeams[i].gameObject.SetActive(false);
            yield return new WaitForSeconds(.5f);
        }
        else
        {
            Audio.PlaySoundAtTransform("Laser2", _laserBeams[i]);
            yield return new WaitForSeconds(.5f);
            _laserBeams[i].gameObject.SetActive(false);
        }
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

    private IEnumerable moveLasersUpDownAnimation(bool up, Color color, params int[] ixs)
    {
        _animating = true;
        var unfinished = 2 * ixs.Length;
        for (int i = 0; i < ixs.Length; i++)
        {
            var ix = ixs[i];

            if (Lasers[ix] == null)
            {
                Lasers[ix] = Instantiate(Lasers[0]);
                Lasers[ix].parent = Hatches[ix].transform;
                Lasers[ix].localRotation = _laserTargetRotations[ix] = Quaternion.Euler(0, Rnd.Range(0f, 360f), 0);
                Lasers[ix].localScale = Lasers[0].localScale;
                Lasers[ix].name = "Laser";
                _laserLenses[ix] = Lasers[ix].Find("Lens");
                _laserBeams[ix] = Lasers[ix].Find("Beam");
                _laserBodies[ix] = Lasers[ix].Find("Body").GetComponent<MeshRenderer>();
            }
            Lasers[ix].gameObject.SetActive(true);

            _isLaserUp[ix] = up;
            if (up)
            {
                float h, s, v;
                Color.RGBToHSV(color, out h, out s, out v);
                _laserBodies[ix].material.color = Color.HSVToRGB(h, s * .25f, (v + 2) / 3);
            }
        }
        for (int i = 0; i < ixs.Length; i++)
        {
            StartCoroutine(_openCloseHatch(ixs[i], up, whenDone: () => { unfinished--; }));
            StartCoroutine(_moveLaser(ixs[i], up, whenDone: () => { unfinished--; }));
            yield return new WaitForSeconds(.1f);
        }
        yield return new WaitUntil(() => unfinished == 0);
        for (int i = 0; i < ixs.Length; i++)
        {
            if (up)
            {
                _laserBeams[ixs[i]].GetComponent<MeshRenderer>().material.color = color;
                _laserBeams[ixs[i]].gameObject.SetActive(true);
                Audio.PlaySoundAtTransform("Laser1", _laserBeams[ixs[i]]);
            }
            else
                Lasers[ixs[i]].gameObject.SetActive(false);
        }
        _animating = false;
    }

    void Restart()
    {
        _stage = 0;
        _hatchesAlreadyPressed.Clear();
        queue.Clear();
        moveLasersUpDown(false, Color.black, Enumerable.Range(0, 9).Where(ix => _isLaserUp[ix]).ToArray());
        LogPermissibleLasers();
    }

    private void LogPermissibleLasers()
    {
        var permissible = Enumerable.Range(0, 9).Where(hatch => IsValid(hatch)).Select(hatch => _laserOrder[hatch]).JoinString(", ");
        Debug.LogFormat("[Lasers #{0}] Stage {1}: Permissible lasers: {2}", _moduleId, _stageNames[_stage], permissible.Length == 0 ? "none! You must incur a strike." : permissible);
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
            if (_hatchesAlreadyPressed.Contains(n))
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
