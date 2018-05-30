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
    private bool canPress = false, isStriking = true, special, activated;
    private List<Renderer> remember = new List<Renderer>(), startOff = new List<Renderer>();
    private readonly List<Color> colorList = new List<Color> { Color.red, orange, Color.yellow, Color.green, Color.blue, purple, Color.white };
    private int r;
    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private int rowRoot, columnRoot, timeRoot, originalTime, moduleParity;
    private string message;
    private List<List<Renderer>> pipeRows = new List<List<Renderer>>(), pipeColumns = new List<List<Renderer>>();

    void Start()
    {
        //Set the main selectables and connections to red, deactivate everything else
        for (int i = 0; i < Connections.Length; i++)
        {
            if (i < 12)
            {
                Connections[i].material.color = Color.red;
            }
            else
            {
                Connections[i].gameObject.SetActive(false);
                //Create a variable I can call later to reset the module
                startOff.Add(Connections[i]);
            }
        }
        for (int i = 0; i < 3; i++)
        {
            //For use with rules that check selections in particular columns or rows
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
        //Prepare to randomize values 
        pipeOrder.AddRange(Enumerable.Range(1, 9));
        //Variable set to make sure there are no cases where the module is unsolvable
        //((This is near impossible, however, so this method may not be necessary.))
        //Instead, the check is used to give valid selections later on in logging
        bool temp = false;
        while (!temp) temp = PipeRandomization();
        //reset r and remember, as Check() wrote to them
        r = 0;
        remember = new List<Renderer>();
        Debug.LogFormat("[Lasers #{0}] Laser order, from left to right: {1}", _moduleId, message);
        Debug.LogFormat("[Lasers #{0}] The laser numbers in the topmost row are {1}", _moduleId, numbers[0].text + ", " + numbers[1].text + ", and " + numbers[2].text);
        Debug.LogFormat("[Lasers #{0}] The digital root is {1}", _moduleId, rowRoot);
        Debug.LogFormat("[Lasers #{0}] Current laser color is red", _moduleId);
        //Valid inputs for each stage - taken from Check()
        Valid();

        for (int i = 0; i < pipes.Length; i++)
        {
            int j = i;
            pipes[i].OnInteract += delegate () { StartCoroutine(Selection(j)); return false; };
        }

        //canPress and activated for module interaction. canPress mostly used for IEnumerator purposes
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
        //message to send to the debug log.
        //Technically pointless, as Check now rewrites to pipeOrder after it's deleted
        message = message.Remove(message.Length - 2, 1);
        //The "autosolver" of the module, technically pointless
        if (Check()) return true;
        else return false;
    }

    bool Check()
    {
        //Rewrite to pipeOrder to use for selection checks
        for (int i = 0; i < pipes.Length; i++)
        {
            pipeOrder.Add(int.Parse(numbers[i].text));
        }
        originalTime = (int)Bomb.GetTime();
        timeRoot = (originalTime / 60) + 1;
        moduleParity = Bomb.GetModuleNames().Count() % 2;
        //Technically int.Parse(numbers[i].text) can be replaced with pipeOrder[i]
        for (int i = 0; i < 3; i++) rowRoot += int.Parse(numbers[i].text);
        for (int i = 1; i < 9; i++)
        {
            if (!(i % 3 == 0)) columnRoot += int.Parse(numbers[i].text);
        }
        while (timeRoot > 9) timeRoot = timeRoot.ToString().ToCharArray().Sum(x => x - '0');
        while (rowRoot > 9) rowRoot = rowRoot.ToString().ToCharArray().Sum(x => x - '0');
        while (columnRoot > 9) columnRoot = columnRoot.ToString().ToCharArray().Sum(x => x - '0');
        //Make sure every rule has at least possible solution
        //The only important index here is check[6], as it's based on all other check[i]'s
        var check = new[] { false, false, false, false, false, false, false };
        //Copy the order list without overriding the original
        var orderCopy = new List<int>();
        //Create a list of available selections per rule
        var available = new List<int>();
        //Create a list of combination selections for all rules
        //Sort of makes var check pointless
        var hold = new List<List<int>>();
        orderCopy = new List<int>(pipeOrder);

        //Check is based on which rule is selected
        //There are 7 rules in all
        for (int i = 0; i < 7; i++)
        {
            //reset available for each rule
            available = new List<int>();
            //Technically doesn't need to be orderCopy
            //Each num represents a physical selection that starts the sequence
            foreach (int num in orderCopy)
            {
                //select grabs the pipe number for position purposes
                //Most calculations here are based on the pipe position anyway
                var select = pipeOrder.IndexOf(num);
                var compare = 0;
                //int i is a replication of int r, which is used to 'remember' which rule we are on
                //technically r could be used here instead, since its values are required for IsAdjacent()
                switch (i)
                {
                    case 0:
                        compare = pipeOrder.IndexOf(rowRoot);
                        if (Rules(i, select, compare))
                        {
                            //available's values are all ints - any zero values would be removed later on if it's the first number
                            //As such, selections are increased by 1 to make sure all values are saved properly.
                            available.Add(select + 1);
                        }
                        break;
                    case 1:
                        //r is set, as it's necessary for IsAdjacent
                        r = 1;
                        //num2 is the selection taken in the previous stage
                        //select is now the second selection in each sequence
                        foreach (int num2 in hold[0])
                        {
                            //Because each available input is +1, num2 must now -1 to match select
                            var temp = num2 - 1;
                            //remember must be used for IsAdjacent. If it doesn't already have a value, add the desired value
                            //if it does have a value, override it with a new value
                            //remember values are based on num2
                            if (remember.Count < 1) remember.Add(centerPipes[temp]);
                            else remember[0] = centerPipes[temp];
                            //select.Equals(temp) is a reimplementation of remember[r].Contains(centerPipes[select])
                            if (!select.Equals(temp) && !IsAdjacent(centerPipes[select]))
                            {
                                //each sequence is written down as a compiled string, then becoming an int
                                //As such, a sequence of press 1 and press 2 would become 12
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 2:
                        compare = pipeOrder.IndexOf(columnRoot);
                        foreach (int num2 in hold[1])
                        {
                            //Taking the numbers apart again, by taking its char and reverting it back to an int
                            var num3 = (num2.ToString().Last() - '0') - 1;
                            var num4 = (num2.ToString().First() - '0') - 1;
                            if (!select.EqualsAny(num3, num4) && Rules(i, select, compare))
                            {
                                available.Add(int.Parse(num2.ToString() + (select + 1).ToString()));
                            }
                        }
                        break;
                    case 3:
                        //set r again for IsAdjacent()
                        r = 3;
                        foreach (int num2 in hold[2])
                        {
                            //This variable is specifically needed for remember
                            //Though, it could be replaced with num1[2] or num1.Last()
                            var num3 = (num2.ToString().Last() - '0') - 1;
                            //All previous values are moved to an array now, to make it easier to check against select
                            var num1 = num2.ToString().ToCharArray().Select(x => (x - '0') - 1).ToArray();
                            if (remember.Count < 3)
                            {
                                //remember values mess up Rules() for Check(), so set to null for now
                                remember[0] = null;
                                //Add another value as IsAdjacent requires remember[2] for this stage
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
                        //messes up Rules() for this case, so set to null 
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
                        //required for IsAdjacent()
                        r = 6;
                        foreach (int num2 in hold[5])
                        {
                            //required for remember
                            var num4 = (num2.ToString()[4] - '0') - 1;
                            var num1 = num2.ToString().ToCharArray().Select(x => (x - '0') - 1).ToArray();
                            if (remember.Count < 5)
                            {
                                //Add for IsAdjacent()
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
            //Add the available sequences for each rule
            //technically only hold[6] is important overall
            hold.Add(new List<int>(available));
            //If a valid sequence exists for each rule, set each value in check to true
            //((However, due to how many sequences there are, there many never be a case where a valid sequence doesn't exist))
            if (hold[i].Count > 0) check[i] = true;
        }
        //Take the final value of sequences and use them later on for logging
        combination = new List<int>(hold[6]);
        //If there are no available sequences for the randomized pipes, try again
        //((Though this may be impossible))
        if (check.Contains(false)) return false;
        else return true;
    }

    //The rules of the module
    //int i is techncially unnecessary, as it's a replication of int r (a global variable)
    //However, it is needed for the Check() method
    //compare isn't always used, due to IsAdjacent and remember's existence
    //Technically, compare could be hardcoded in, since all values are stationary
    //(pipeOrder.IndexOf(rowRoot)), for example
    bool Rules(int i, int select, int compare)
    {
        switch (i)
        {
            //All rules have a check for remember, with the exception of rule 0, as remember does not exist
            //These aren't needed in Check(), as Check has its own remember values
            //Check() goes through selections starting at each selection per rule, so remember would always be changing anyway 
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
        //Available will list the numbered values of pipes that are selectable
        var available = new List<int>();
        foreach (int num in combination)
        {
            var num2 = num.ToString();
            //Take all values based on the current rule
            var num3 = num2[r] - '0' - 1;
            if (remember.Count() > 0)
            {
                //Find the values in Combination and Remember that match
                //This will be the proper sequence based on what the player has actually selected
                var num4 = remember.Select(x => (x.name[4] - '0')).ToArray();
                var num5 = num2.ToCharArray().Select(x => x - '0').ToArray();
                var num6 = new List<bool>();
                for (int j = 0; j < num4.Count(); j++)
                {
                    if (num4[j].Equals(num5[j])) num6.Add(true);
                    else num6.Add(false);
                }
                //Add num3 to available
                //Make sure available doesn't already have num3, there are a lot of repeats
                //Make sure that num3 hasn't already been selected
                //Make sure that all selected values matches the generated values
                if (!available.Contains(num3) && !remember.Contains(centerPipes[num3]) && !num6.Contains(false)) available.Add(num3);
            }
            else
            {
                //This is only valid during the first rule
                //Technicaly !available.contains(num3) isn't needed here as each value is only listed once for the first rule
                if (!available.Contains(num3)) available.Add(num3);
            }
        }
        //Let's the player know they've ran into a dead end by their own fault
        //This is actually possible, unlike a generation with no solution
        if (available.Count < 1)
        {
            Debug.LogFormat("[Lasers #{0}] No acceptable selections available. A Strike is necessary.", _moduleId);
            //Different logging message for dead ends
            special = true;
            return;
        }
        //Available is based on laser position. Message translates this to the laser's actual values
        message = String.Join(", ", available.Select(x => numbers[x].text).ToArray());
        Debug.LogFormat("[Lasers #{0}] Acceptable selections are {1}", _moduleId, message);
    }

    IEnumerator Selection(int i)
    {
        //No button interaction if module is not activated
        if (!activated) yield break;
        //Pause selections while the coroutine is running (probably buggy)
        while (!canPress) yield return null;
        //isStriking is used mostly for TP
        isStriking = false;
        //Fade the selected laser and connected connections to black (based on rule r)
        var a = Connections.Where(x => x.name.Contains((i + 1).ToString()));
        var b = a.Concat(new[] { centerPipes[i] });
        //Fade the reset of the lasers to the next color in the sequence
        var c = Connections.Where(x => !x.name.Contains((i + 1).ToString()) && x.gameObject.activeSelf);
        var d = c.Concat(centerPipes.Where(x => !x.Equals(centerPipes[i]) && !remember.Contains(x)));
        //This actually doesn't do anything, it's a placeholder for later
        var e = Fade(Connections.Concat(centerPipes).ToArray(), Connections.Concat(centerPipes).ToArray().Count(), 0, 1.5f, Color.red, Color.black);
        //Stops further interactions until canPress is true again (hopefully)
        canPress = false;
        //See Fade() for details on the following cases
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
                    //Rather than copying the same code for each case, put it all in case 9
                    goto case 9;
                }
                break;
            case 1:
                //Since int compare isn't really used, 0 is sent to the function
                //compare could probably be hardcoded into Rules() as they would be the same for Check() and Selection()
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
                    //Make the lasers show up based on remember and have them link into each other one after the other
                    //Technically it's supposed to look like each laser is shooting into one another, and each selection changes the color of the laser
                    //But right now, all it does is fade the objects in
                    //I feel like this could look really cool down the line
                    //TODO I guess
                    StartCoroutine(Finale());
                }
                else
                {
                    e = Fade(b.Concat(d).ToArray(), b.Count(), d.Count(), 1f, Color.white, new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, new Color(0.75f, 0.75f, 0.75f, 0.5f));
                    goto case 9;
                }
                break;
            case 7:
                //Since r increases for rule 6, have this here to let people know
                //Yes, your input has been detected. But you can stop now
                Debug.LogFormat("[Lasers #{0}] Module completed", _moduleId);
                goto end;
            case 9:
                while (e.MoveNext())
                {
                    yield return e.Current;
                }
                Module.HandleStrike();
                //Moved to a separate function
                //The color of b and d are set in the else parts of each case
                //since it's hard to grab those values, I just grab them from the first value in each list 
                //b and d are combined as the list of connections that need to change colors need to be redetermined
                Restart(i, b.Concat(d).ToList(), b.ToList()[0].material.color, d.ToList()[0].material.color);
                //Reset r and remember since they're kind of important for keeping track of the stage and selections
                r = 0;
                remember = new List<Renderer>();
                break;
        }
        //This is kind of here to make the selections connect to the outer walls if all its surrounding selections are inactive
        //However, it's very buggy.
        PipeCheck();
        canPress = true;
        end: yield break;
    }

    void Restart(int i, List<Renderer> a, Color colorFade, Color colorIntent)
    {
        //To let TP know to stop its coroutine
        isStriking = true;
        //Select the connections/selections where the color is between the previous color and black
        //Probably buggy
        var b = a.Where(x => x.material.color.Equals(colorFade) && !startOff.Contains(x)).ToList();
        //Select the connections/selections where the color is between the previous color and the intended color
        //Probably buggy
        var c = a.Where(x => x.material.color.Equals(colorIntent) && !startOff.Contains(x)).ToList();
        //Grab any connections that are missing from lists b/d (list a here) and combine them with all remember values
        //These should be the previously faded out renderers, which were disabled in Fade()
        var d = Connections.Where(x => !startOff.Contains(x)).Concat(remember);
        //Start by bringing back the faded selections
        var e = Fade(d.ToArray(), d.Count(), 0, 0.5f, Color.black, Color.red);
        StartCoroutine(e);
        //Fade the fading selections back to red
        e = Fade(b.ToArray(), b.Count(), 0, 0.5f, colorFade, Color.red);
        StartCoroutine(e);
        //Fade the rest of the selections to red
        e = Fade(c.ToArray(), c.Count(), 0, 0.5f, colorIntent, Color.red);
        StartCoroutine(e);
        //Turn off any extra connections caused by PipeCheck() [Hopefully]
        e = Fade(startOff.ToArray(), startOff.Count(), 0, 0.5f, colorFade, Color.black);
        StartCoroutine(e);
        //Different messages depending on dead end or just plain wrong laser
        if (!special) Debug.LogFormat("[Lasers #{0}] Invalid laser selected", _moduleId);
        else Debug.LogFormat("[Lasers #{0}] Laser selected, module has been reset", _moduleId);
        Debug.LogFormat("[Lasers #{0}] Current laser color is red", _moduleId);
        special = false;
    }

    //Connect lasers to wall if all adjacent lasers are inactive
    //Ex. Top middle and middle left inactive for the top left selection
    //Top left selection would connect up and left
    //This could probably look cooler but I don't really know what to do with it.
    //Also probably buggy
    //Pipe 5 (middle) has a special case where it will connect diagonally if all connections are inactive
    void PipeCheck()
    {
        foreach (Renderer ren in centerPipes)
        {
            var a = ren.material.color;
            if (ren.gameObject.activeSelf && !Connections.Where(x => x.name.Contains(ren.name[4].ToString())).Select(x => x.gameObject.activeSelf).Contains(true))
            {
                switch (ren.name[4] - '0')
                {
                    case 1:
                        //Connections are hardcoded here. Probably could be done better
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
                    case 5:
                        if (!Connections.Select(x => x.gameObject.activeSelf).Contains(true))
                        {
                            if (centerPipes[0].gameObject.activeSelf)
                            {
                                Connections[12].gameObject.SetActive(true);
                                Connections[12].material.color = a;
                            }
                            if (centerPipes[2].gameObject.activeSelf)
                            {
                                Connections[15].gameObject.SetActive(true);
                                Connections[15].material.color = a;
                            }
                            if (centerPipes[6].gameObject.activeSelf)
                            {
                                Connections[16].gameObject.SetActive(true);
                                Connections[16].material.color = a;
                            }
                            if (centerPipes[8].gameObject.activeSelf)
                            {
                                Connections[18].gameObject.SetActive(true);
                                Connections[18].material.color = a;
                            }
                        }
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

    //Essentially another "Rules()" function, and also why Rules() is mostly missing from Check()
    //This checks if a particular laser is adjacent to a remembered laser for a specific rule
    bool IsAdjacent(Renderer check)
    {
        //remember doesn't exist when the module starts (outside of in Check(), but even that is reset before activation)
        if (r.Equals(0)) return false;
        //copy is the laser we check against the selection
        //This is why the remember variable is written to in Check, as there's no real way to Check adjacent lasers otherwise
        var copy = remember[0];
        if (r.Equals(3)) copy = remember[2];
        if (r.Equals(6)) copy = remember[4];
        //Rules 1, 3, and 6 are the only rules based on adjacency
        //Rule 1 is based on UDLR
        //Rule 3 is based on Diagonal position
        //Rule 6 is both
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
                //Random fact: Pipe5 can't be selected for rule 4, due to the fact it's adjacent to all lasers
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

    //A quick function for the log
    //It's used for rules that are based on a particular color's position
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

    //The function responsible for all the fading
    //rendererList is a list of objects to fade. Sometimes, it consists of two different lists that are concated
    //rLC1 is the count of the first list (or the entire list)
    //rLC2 is the count of the second list, or 0 if there is none
    //float time is the time it takes for the object to fade
    //colorOriginal1 and colorFinal1 are for fading from one color to another. This usually only effects the first list
    IEnumerator Fade(Renderer[] rendererList, int rLC1, int rLC2, float time, Color colorOriginal1, Color colorFinal1, Color? colorOriginal2 = null, Color? colorFinal2 = null)
    {
        //Sometimes two different sets are faded at the same time, but there are cases where only one set is fading
        //as such multiple color values aren't always needed.
        var colorOriginalT = colorOriginal2 ?? Color.black;
        var colorFinalT = colorFinal2 ?? Color.black;
        for (int i = 0; i < rendererList.Count(); i++)
        {
            //If an object that is desired to be unfaded is inactive, make it active
            //This should check both lists if they were concated together when the Fade function was called
            if ((colorOriginal1.Equals(Color.black) && i < rLC1) || (colorOriginal2.Equals(Color.black) && rLC2 > 0 && i >= rLC1) ) rendererList[i].gameObject.SetActive(true);
        }
        for (float t = 0.0f; t < time; t += Time.deltaTime)
        {
            for (int i = 0; i < rLC1; i++)
            {
                rendererList[i].material.color = Color.Lerp(colorOriginal1, colorFinal1, t / time);
            }
            yield return null;
            //no need to run the fade on the second list if the second list count is 0
            //Though it does need to be separate
            if (rLC2 > 0)
            {
                for (int i = rLC1; i < rendererList.Count(); i++)
                {
                    rendererList[i].material.color = Color.Lerp(colorOriginalT, colorFinalT, t / time);
                }
            }
        }
        //If the object is fading to black, make it inactive
        for (int i = 0; i < rendererList.Count(); i++)
        {
            if ((colorFinal1.Equals(Color.black) && i < rLC1) || (colorFinal2.Equals(Color.black) && rLC2 > 0 && i >= rLC1)) rendererList[i].gameObject.SetActive(false);
        }
    }

    //Make the lasers do something 'cool' at the end
    //Not really finished
    IEnumerator Finale()
    {
        for (int i = 0; i < remember.Count; i++)
        {
            //Fade requires an array, but oh well, just feed it on item
            var fade = Fade(new[] { remember[i] }, 1, 0, 0.5f, Color.black, colorList[i]);
            while (fade.MoveNext()) yield return fade.Current;
            if (i < remember.Count - 1)
            {
                //Eventually this will be moved elsewhere to make it look like each laser is shooting into one another
                //But it's far too complicated for me to implement right now
                //So just fade everything in instead
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
            //You entered the same value multiple times, this is no good.
            if (remember.Contains(centerPipes[n])) yield break;
            yield return new KMSelectable[] { pipes[n] };
            yield return null;
            //Don't move to the next selection until the previous selection is complete
            yield return new WaitUntil(() => canPress);
            //idk honestly, but we do want this to stop if a strike is being dealt
            if (isStriking) StopAllCoroutines();
        }
    }
}
