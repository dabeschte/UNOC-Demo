using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;


[AttributeUsage(AttributeTargets.Field, Inherited = true)]
public class ReadOnlyAttribute : PropertyAttribute { }
#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyAttributeDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect rect, UnityEditor.SerializedProperty prop, GUIContent label)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(rect, prop);
        GUI.enabled = wasEnabled;
    }
}
#endif

[System.Serializable]
public class BVHJoint
{
    public enum Channel
    {
        Xposition,
        Yposition,
        Zposition,
        Xrotation,
        Yrotation,
        Zrotation
    };
    public string name;
    public Vector3 offset;
    public List<Channel> channels = new List<Channel>();
    public int id0;
    [NonSerialized] public BVHJoint parent = null;
    [NonSerialized] public List<BVHJoint> children = new List<BVHJoint>();
    

    public void addChild(BVHJoint joint)
    {
        children.Add(joint);
        joint.parent = this;
    }

    public bool positionAnimated()
    {
        return channels.Any(x => x <= Channel.Zposition);
    }

    public bool rotationAnimated()
    {
        return channels.Any(x => x >= Channel.Xrotation);
    }
}

public class BVHAnimator : MonoBehaviour
{
    public bool autoPlay = true;
    public bool applyRootTransform = false;
    public bool applyPosition = true;
    public bool convertOnTheFly = true;
    public bool useGlobalRotations = true;
    public bool useGlobalPositions = true;
    public bool applyInitialRotation = true;
    public bool scaleBones = true;
    public bool interpolate = true;
    public float positionScale = 1;
    public string path;
    [ReadOnly] public int fps = 0;
    [ReadOnly] public int frames = 0;
    public int currentFrame = 0;
    public float speed = 1.0f;
    Dictionary<string, BVHJoint> joints = new Dictionary<string, BVHJoint>();
    List<float[]> vals = new List<float[]>();
    List<string> valStrings = new List<string>();
    Dictionary<string, Quaternion> tposeQuats = new Dictionary<string, Quaternion>();
    Dictionary<string, Vector3> tposePos = new Dictionary<string, Vector3>();
    Dictionary<string, Transform> jointTransforms = new Dictionary<string, Transform>();
    BVHJoint root;

    float animStartTime = 0.0f;
    float animScaledTime = 0.0f;
    public bool loadFromResources = true;
    public string resourceName = "TABLE_sit_grabCup_lookAtCup_drinkCup_putDownCup_merged.bvh";


    bool isPlaying = false;
    List<Quaternion> q0 = new List<Quaternion>();


    void parseHierarchy(string hierarchy)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

        var channelNames = Enum.GetNames(typeof(BVHJoint.Channel)).Select(x => x.ToLower()).ToList();
        Stack<BVHJoint> jointStack = new Stack<BVHJoint>();
        int idn = 0;
        foreach (var line in hierarchy.Split('\n'))
        {
            var l = line.TrimStart().TrimEnd();
            if (Regex.IsMatch(l, @"^(ROOT|JOINT|END)", RegexOptions.IgnoreCase))
            {
                var name = Regex.Split(l, @"^(ROOT|JOINT|END)", RegexOptions.IgnoreCase)[2].Trim();

                var joint = new BVHJoint();
                if (jointStack.Count > 0)
                {
                    var parent = jointStack.Peek();
                    parent.addChild(joint);
                }
                else
                {
                    root = joint;
                }
                joint.id0 = idn;
                joint.name = Regex.IsMatch(l, "^END", RegexOptions.IgnoreCase) ? joint.parent.name + "_end" : name;
                jointStack.Push(joint);
                joints[joint.name] = joint;
            } 
            else if (Regex.IsMatch(l, "OFFSET", RegexOptions.IgnoreCase))
            {
                var offStr = Regex.Split(Regex.Split(l, @"OFFSET\s+", RegexOptions.IgnoreCase)[1], "\\s+");
                var off = new Vector3(float.Parse(offStr[0]), float.Parse(offStr[1]), float.Parse(offStr[2]));
                jointStack.Peek().offset = off;
            }
            else if (Regex.IsMatch(l, "CHANNELS", RegexOptions.IgnoreCase))
            {
                var channelCount = int.Parse(Regex.Split(l, @"CHANNELS\s+", RegexOptions.IgnoreCase)[1][0].ToString());
                if (channelCount == 0)
                    continue;
                var channelsStr = Regex.Split(Regex.Split(l, channelCount.ToString() + "\\s+")[1], "\\s+");
                foreach (var channelstr in channelsStr)
                {
                    var c = channelstr.ToLower();
                    var channel = (BVHJoint.Channel) channelNames.FindIndex(x => x == c);
                    jointStack.Peek().channels.Add(channel);
                }

                idn += jointStack.Peek().channels.Count;
            }
            else if (Regex.IsMatch(l, @"\}", RegexOptions.IgnoreCase))
            {
                jointStack.Pop();
            }
        }
    }

    void parseMotion(string motion)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        var lines = motion.Split('\n');

        int lineIdx = 0;
        for (;; ++lineIdx)
        {
            var l = lines[lineIdx].TrimStart().TrimEnd();
            if (Regex.IsMatch(l, @"FRAMES:\s*", RegexOptions.IgnoreCase))
            {
                frames = Int32.Parse(Regex.Split(l, @"FRAMES:\s*", RegexOptions.IgnoreCase)[1]);
            } 
            else if (Regex.IsMatch(l, @"FRAME TIME:\s*", RegexOptions.IgnoreCase))
            {
                fps = (int) Math.Round(1.0 / float.Parse(Regex.Split(l, @"FRAME TIME:\s*", RegexOptions.IgnoreCase)[1]));
            }
            else if (!Regex.IsMatch(l, @"^\s*$"))
            {
                break;
            }
        }

        if (convertOnTheFly)
        {
            valStrings = lines.Skip(lineIdx).ToList();
        }
        else
        {
            for (; lineIdx < lines.Length; lineIdx++)
            {
                var l = lines[lineIdx].TrimStart().TrimEnd();
                if (Regex.IsMatch(l, @"^\s*$"))
                {
                    break;
                }
                var frameVals = Array.ConvertAll(Regex.Split(l, @"\s+"), float.Parse);
                vals.Add(frameVals);
            }
        }
    }

    void getJointTransforms()
    { 
        foreach (var bvhJoint in joints.Values)
        {
            var trans = FindObjectsOfType<GameObject>().FirstOrDefault(g => g.name == bvhJoint.name && g.transform.root.gameObject == gameObject);
            if (trans)
            {
                jointTransforms[bvhJoint.name] = trans.transform;
            }
        }
    }

    void getTPoseRotations()
    {
        foreach (var jointTransform in jointTransforms)
        {
            if (joints[jointTransform.Key].rotationAnimated())
            {
                tposeQuats[jointTransform.Key] = jointTransform.Value.rotation;
            }
        }
    }

    void getTPosePositions()
    {
        foreach (var jointTransform in jointTransforms)
        {
            if (joints[jointTransform.Key].positionAnimated())
            {
                tposePos[jointTransform.Key] = jointTransform.Value.localPosition;
            }
        }
    }

    public void applyFrame(int frameIdx, float t_interpolate)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        currentFrame = frameIdx;
        int nextFrameIdx = Math.Min(currentFrame + 1, frames - 1);
        var splitArr = Regex.Split(valStrings[frameIdx].TrimStart().TrimEnd(), @"\s+");
        if (splitArr.Length < joints.Last().Value.id0)
            return;

        var frameVals = convertOnTheFly
            ? Array.ConvertAll(splitArr, float.Parse)
            : vals[frameIdx];

        var splitArrNext = Regex.Split(valStrings[nextFrameIdx].TrimStart().TrimEnd(), @"\s+");
        if (splitArrNext.Length < joints.Last().Value.id0)
            return;

        var frameValsNext = convertOnTheFly
            ? Array.ConvertAll(splitArrNext, float.Parse)
            : vals[nextFrameIdx];

        Dictionary<string, Quaternion> parentRotation = new Dictionary<string, Quaternion>();

        var iterable = tposeQuats;

        foreach (var tposeQuat in iterable)
        {
            var joint = joints[tposeQuat.Key];
            if (joint == root && !applyRootTransform)
            {
                parentRotation[joint.name] = Quaternion.identity;
                continue;
            }

            var trans = jointTransforms[tposeQuat.Key];
            var quat0 = tposeQuat.Value;

            var frameQuat = getFrameQuat(joint, frameVals);
            if (interpolate)
            {
                var frameQuatNext = getFrameQuat(joint, frameValsNext);
                frameQuat = Quaternion.Lerp(frameQuat, frameQuatNext, t_interpolate);
            }

            if (joint.parent != null)
            {
                if (!parentRotation.ContainsKey(joint.parent.name))
                    print(joint.parent.name);
                frameQuat = parentRotation[joint.parent.name] * frameQuat;
            }
            else if (!useGlobalRotations)
            {
                frameQuat = transform.rotation * frameQuat;
            }

            parentRotation[joint.name] = frameQuat;

            if (applyInitialRotation)
                trans.rotation = frameQuat * quat0;
            else
                trans.rotation = frameQuat;
        }

        if (applyPosition)
        {
            foreach (var tposePos in tposePos)
            {
                var joint = joints[tposePos.Key];
                if (joint == root && !applyRootTransform)
                    continue;

                var trans = jointTransforms[tposePos.Key];

                var framePos = getFramePos(joint, frameVals);
                if (interpolate)
                {
                    var framePosNext = getFramePos(joint, frameValsNext);
                    framePos = Vector3.Lerp(framePos, framePosNext, t_interpolate);
                }

                if (useGlobalPositions)
                {
                    trans.position = framePos;
                }
                else
                {
                    trans.localPosition = framePos;
                }
            }
        }
    }

    private Vector3 getFramePos(BVHJoint joint, float[] frameVals)
    {
        var framePos = Vector3.zero;
        for (int i = 0; i < joint.channels.Count; i++)
        {
            var c = joint.channels[i];
            var v = frameVals[joint.id0 + i] / positionScale;
            var p = Vector3.zero;
            switch (c)
            {
                case BVHJoint.Channel.Xposition:
                    p = new Vector3(-v, 0, 0);
                    break;
                case BVHJoint.Channel.Yposition:
                    p = new Vector3(0, v, 0);
                    break;
                case BVHJoint.Channel.Zposition:
                    p = new Vector3(0, 0, v);
                    break;
            }

            framePos = framePos + p;
        }

        return framePos;
    }

    private static Quaternion getFrameQuat(BVHJoint joint, float[] frameVals)
    {
        var frameQuat = Quaternion.identity;

        for (int i = 0; i < joint.channels.Count; i++)
        {
            var c = joint.channels[i];
            var v = frameVals[joint.id0 + i];
            var q = Quaternion.identity;
            switch (c)
            {
                case BVHJoint.Channel.Xrotation:
                    q = Quaternion.Euler(v, 0, 0);
                    break;
                case BVHJoint.Channel.Yrotation:
                    q = Quaternion.Euler(0, -v, 0);
                    break;
                case BVHJoint.Channel.Zrotation:
                    q = Quaternion.Euler(0, 0, -v);
                    break;
            }

            frameQuat = frameQuat * q;
        }

        return frameQuat;
    }

    void scaleAvatarBones()
    { 
        // TODO
    }

    public void loadAnim(string animPath, bool fromResources=false)
    {
        path = animPath;
        string fileData = null;
        if (fromResources)
        {
            fileData = Resources.Load<TextAsset>(animPath).text;
        }
        else
        {
            fileData = System.IO.File.ReadAllText(path);
        }
        string hierarchy = Regex.Split(fileData, "MOTION")[0];
        string motion = Regex.Split(fileData, "MOTION")[1];
        currentFrame = 0;

        parseHierarchy(hierarchy);
        parseMotion(motion);
        if (scaleBones)
            scaleAvatarBones();
    }

    // Start is called before the first frame update
    void Awake()
    {
        if (autoPlay)
        {
            isPlaying = true;
            animStartTime = Time.time;
            animScaledTime = animStartTime;
        }

        if (loadFromResources)
        {
            loadAnim(resourceName, true);
        }
        else
        {
            loadAnim(path, false);
        }
        getJointTransforms();
        getTPoseRotations();
        getTPosePositions();
    }

    public void loadAnimFromResources(string name)
    {
        loadAnim(name, true);
        animScaledTime = 0.0f;
        isPlaying = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (isPlaying)
        {
            var oldFrame = currentFrame;
            animScaledTime += Time.deltaTime * speed;
            currentFrame = (int) (fps * animScaledTime);
            float t_interpolate = (fps * animScaledTime) % 1.0f;
            currentFrame = Math.Min(currentFrame, frames - 1);
            currentFrame = Math.Max(currentFrame, 0);
            if (currentFrame >= frames - 1 && oldFrame != 0)
            {
                currentFrame = 0;
                animStartTime = Time.time;
                animScaledTime = Time.time;
                Update();
            }
            applyFrame(currentFrame, t_interpolate);
        }
    }
}
