
/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Pipes;
using UnityEngine;

public class PipesModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMSelectable[] pipes;
    public Renderer[] Connections, centerPipes;
    public TextMesh[] numbers;
    static Color orange = new Color(0, 0.5f, 1f), purple = new Color(0.5f, 0, 0.5f);

    private List<int> pipeOrder = new List<int>();
    private bool canPress = false, isStriking = true, Diagonal;
    private Color a;
    private List<Renderer> remember = new List<Renderer>(), forget = new List<Renderer>();
    private List<Color> colorList = new List<Color> { Color.red, orange, Color.yellow, Color.green, Color.blue, purple, Color.white };
    private int r;
    private Queue Order = new Queue();
    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private int rowRoot, columnRoot, timeRoot, originalTime;
    private string message;
    private string[] rules = new string[7];

    void Start()
    {
        for (int i = 0; i < Connections.Length; i++)
        {
            if (i < 24)
            {
                if (i < 9)
                {
                    centerPipes[i].material.color = Color.red;
                }
                Connections[i].material.color = Color.red;
            }
            else
            {
                Connections[i].gameObject.SetActive(false);
            }
        }
    }

    void Awake()
    {
        _moduleId = _moduleIdCounter++;
        Debug.LogFormat("[Pipes #{0}] Initializing...", _moduleId);
        Debug.LogFormat("[Pipes #{0}] Randomizing pipes...", _moduleId);
        pipeOrder.AddRange(Enumerable.Range(1, 9));
        foreach (TextMesh text in numbers)
        {
            var i = UnityEngine.Random.Range(0, pipeOrder.Count);
            text.text = pipeOrder[i].ToString();
            pipeOrder.RemoveAt(i);
            message += text.text + ", ";
        }
        message = message.Remove(message.Length - 2, 1);
        Debug.LogFormat("[Pipes #{0}] Pipe order, from left to right: {1}", _moduleId, message);
        Debug.LogFormat("[Pipes #{0}] Current pipe color is red", _moduleId);
        Module.OnActivate += delegate () { Rules(); };

        for (int i = 0; i < pipes.Length; i++)
        {
            int j = i;
            pipes[i].OnInteract += delegate () { Selection(j); return false; };
        }
    }

    void Rules()
    {
        for (int i = 0; i < pipes.Length; i++)
        {
            pipeOrder.Add(int.Parse(numbers[i].text));
        }
        originalTime = (int)Bomb.GetTime();
        timeRoot = (originalTime / 60) + 1;
        for (int i = 0; i < 3; i++) rowRoot += int.Parse(numbers[i].text);
        while (timeRoot > 9) timeRoot = timeRoot.ToString().ToCharArray().Sum(x => x - '0');
        while (rowRoot > 9) rowRoot = rowRoot.ToString().ToCharArray().Sum(x => x - '0');
        Debug.LogFormat("[Pipes #{0}] The pipe numbers in the topmost row are {1}", _moduleId, numbers[0].text + ", " + numbers[1].text + ", and " + numbers[2].text );
        Debug.LogFormat("[Pipes #{0}] The digital root is {1}", _moduleId, rowRoot);
        var forbiddenPipe = ForbiddenPipe(0);
        Debug.LogFormat("[Pipes #{0}] The {0} pipe is the forbidden pipe", _moduleId, forbiddenPipe);
        canPress = true;
    }

    void Selection(int i)
    {
        switch (r)
        {
            case 0:
                if (numbers[i].text == rowRoot.ToString()) isStriking = false;
                Debug.LogFormat("[Pipes #{0}] ", _moduleId);
                break;
        }
        if (!isStriking) Order.Enqueue(StartCoroutine(Fade(i)));
        else Order.Enqueue(StartCoroutine(FakeFade(i)));
    }

    string PipePosition(int position)
    {
        switch (position)
        {
            case 1:
                message = "top left";
                break;
            case 2:
                message = "top middle";
                break;
            case 3:
                message = "top right";
                break;
            case 4:
                message = "middle left";
                break;
            case 5:
                message = "middle";
                break;
            case 6:
                message = "middle right";
                break;
            case 7:
                message = "bottom left";
                break;
            case 8:
                message = "bottom middle";
                break;
            case 9:
                message = "bottom right";
                break;
        }
        return message;
    }

    string ForbiddenPipe(int stage)
    {
        if (stage == 0)
        {
            if (pipeOrder.Contains(rowRoot))
            {
                var avoid = pipeOrder.IndexOf(rowRoot);
                message = pipes[avoid].name.Replace("Pipe", "");
            }
        }
        switch (message)
        {
            case "1":
                rules[0] = "1 2 3";
                if (stage > 1)
                {
                    if (remember[0].Equals(centerPipes[0])) rules[1] = "2 4";
                }
                rules[2]
                break;
        }
    }

    IEnumerator FakeFade(int s)
    {
        isStriking = true;
        canPress = false;
        for (float t = 0.0f; t < 0.75f; t += Time.deltaTime)
        {
            var alpha = colorList[r];
            alpha.a = 50;
            for (int i = 0; i < 9; i++)
            {
                if (i == s) centerPipes[s].material.color = Color.Lerp(colorList[r], new Color(0f, 0f, 0f, 0), t / 1.5f);
                else centerPipes[i].material.color = Color.Lerp(colorList[r], alpha, t / 0.75f);
            }

            for (int j = 0; j < Connections.Length; j++)
            {
                if (Connections[j].name.Contains((s + 1).ToString())) Connections[j].material.color = Color.Lerp(colorList[r], new Color(0f, 0f, 0f, 0), t / 1.5f);
                else Connections[j].material.color = Color.Lerp(colorList[r], alpha, t / 0.75f);
            }
            yield return null;
        }
        Module.HandleStrike();
        foreach (Renderer p in remember) p.gameObject.SetActive(true);
        for (float t = 0.0f; t < 0.25f; t += Time.deltaTime)
        {
            for (int i = 0; i < 23; i++)
            {
                var alpha = Connections[i].material.color;
                Connections[i].material.color = Color.Lerp(alpha, Color.red, t / 0.25f);
                if (i < 9)
                {
                    alpha = centerPipes[i].material.color;
                    centerPipes[i].material.color = Color.Lerp(alpha, Color.red, t / 0.25f);
                }
            }
            yield return null;
        }
        remember.Clear();
        r = 0;
        canPress = true;
    }

    IEnumerator Fade(int s)
    {
        if (!canPress) yield break;
        canPress = false;
        if (r < 6)
        {
            remember.Add(centerPipes[s]);
            for (float t = 0.0f; t < 1.5f; t += Time.deltaTime)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (i == s) centerPipes[s].material.color = Color.Lerp(colorList[r], new Color(0f, 0f, 0f, 0), t / 1.5f);
                    else centerPipes[i].material.color = Color.Lerp(colorList[r], colorList[r + 1], t / 1.5f);
                }

                for (int j = 0; j < Connections.Length; j++)
                {
                    if (Connections[j].name.Contains((s + 1).ToString())) Connections[j].material.color = Color.Lerp(colorList[r], new Color(0f, 0f, 0f, 0), t / 1.5f);
                    else Connections[j].material.color = Color.Lerp(colorList[r], colorList[r + 1], t / 1.5f);
                }
                yield return null;
            }
            pipes[s].gameObject.SetActive(false);
            centerPipes[s].material.color = colorList[r];
            r++;
            canPress = true;
            Order.Dequeue();
        }
        else if (r == 6)
        {
            remember.Add(centerPipes[s]);
            for (float t = 0.0f; t < 1.5f; t += Time.deltaTime)
            {
                for (int i = 0; i < pipes.Length; i++)
                {
                    centerPipes[i].material.color = Color.Lerp(colorList[r], new Color(0, 0, 0, 0), t / 1.5f);
                }
                for (int j = 0; j < 72; j++)
                {
                    Connections[j].material.color = Color.Lerp(colorList[r], new Color(0, 0, 0, 0), t / 1.5f);
                }
                yield return null;
            }
            for (int i = 0; i < 7; i++)
            {
                remember[i].gameObject.SetActive(true);
                for (float t = 0.0f; t < 0.5f; t += Time.deltaTime)
                {
                    for (int j = 0; j < Connections.Length; j++)
                    {
                        if (Connections[j].name.Contains(remember[i].name[4]))
                        {
                            Connections[j].material.color = Color.Lerp(new Color(0, 0, 0, 0), colorList[i], t / 0.5f);
                        }
                    }
                    remember[i].material.color = Color.Lerp(new Color(0, 0, 0, 0), colorList[i], t / 0.5f);
                    yield return null;
                }
                //playsound "ding"
            }
            Module.HandlePass();
        }
    }
}
*/