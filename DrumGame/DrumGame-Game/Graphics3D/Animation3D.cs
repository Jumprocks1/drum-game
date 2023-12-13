using glTFLoader.Schema;
using osuTK;

namespace DrumGame.Game.Graphics3D;

public class Frame
{
    public JointState[] Joints;
    public Frame(int count)
    {
        Joints = new JointState[count];
        for (int i = 0; i < count; i++)
        {
            Joints[i] = new JointState();
        }
    }
}
public class Animation3D
{
    public Frame[] Frames;
    public int FrameCount => Frames.Length;

    public void ApplyTo(Skin3D skin, double t)
    {
        // input values are times
        // input accessor looks like: 0, 1/24, 2/24, 3/24, etc
        // output are the transforms at that time

        var start = 0;
        // var start = 250;
        // var end = 281;
        // count = end - start + 1;

        t %= FrameCount;

        // 248 through 284

        int i1 = (int)t;
        int i2 = i1 + 1;

        i1 %= FrameCount;
        i2 %= FrameCount;

        var blend = (float)(t - i1);

        i1 += start;
        i2 += start;

        var frame1 = Frames[i1].Joints;
        var frame2 = Frames[i2].Joints;

        // Console.WriteLine($"{i1} {i2} {blend}");

        for (int i = 0; i < frame1.Length; i++)
        {
            var boneInfo = skin.Joints[i].State;
            boneInfo.Translation = Vector3.Lerp(frame1[i].Translation, frame2[i].Translation, blend);
            boneInfo.Rotation = Quaternion.Slerp(frame1[i].Rotation, frame2[i].Rotation, blend);
            boneInfo.Scale = Vector3.Lerp(frame1[i].Scale, frame2[i].Scale, blend);
        }
    }

    public Animation3D(Animation animation, Skin3D skin, glTFScene scene)
    {
        var model = scene.model;
        var BoneMapping = skin.BoneMapping;
        // input values are times
        // input accessor looks like: 0, 1/24, 2/24, 3/24, etc
        // output are the transforms at that time

        var frameCount = model.Accessors[animation.Samplers[0].Input].Count;
        Frames = new Frame[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            var frame = Frames[i] = new Frame(skin.Joints.Length);
            foreach (var c in animation.Channels)
            {
                var target = c.Target;
                var prop = target.Path;
                var sampler = animation.Samplers[c.Sampler];
                var output = model.Accessors[sampler.Output];

                var nodeId = target.Node.Value;
                if (prop == AnimationChannelTarget.PathEnum.weights)
                {
                }
                else
                {
                    var boneId = BoneMapping[nodeId];
                    var state = frame.Joints[boneId];
                    if (prop == AnimationChannelTarget.PathEnum.translation)
                    {
                        state.Translation = scene.GetVector3(output, i);
                    }
                    else if (prop == AnimationChannelTarget.PathEnum.rotation)
                    {
                        state.Rotation = scene.GetQuat(output, i);
                    }
                    else if (prop == AnimationChannelTarget.PathEnum.scale)
                    {
                        state.Scale = scene.GetVector3(output, i);
                    }
                }
            }
        }
    }
}
