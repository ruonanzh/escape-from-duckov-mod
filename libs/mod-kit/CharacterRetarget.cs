// CharacterRetarget.cs —— 保留源骨架的运行时旋转重定向。
//
// 游戏 Avatar 是 Generic，不能用 HumanPoseHandler。这里也不复制骨骼位置或改变骨长：
// Animator 先驱动鸭子骨骼；本模块在 LateUpdate 读取每根骨相对 bind pose 的世界旋转增量，
// 再把同一个增量施加到源关节的 bind pose 上。源网格与源权重保持原样。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    /// <summary>一副源骨架与一副游戏驱动骨架之间的运行时绑定。由 CharacterSwap 创建与销毁。</summary>
    public sealed class CharacterRetarget : IDisposable
    {
        sealed class Entry
        {
            public Transform Transform;
            public int Node = -1;
            public int Joint = -1;
            public int Driver = -1;
            public Quaternion SourceRest = Quaternion.identity;
            public Quaternion DriverRest = Quaternion.identity;
        }

        Transform _space;
        Transform _root;
        Entry[] _entries = new Entry[0];
        Transform[] _sourceBones = new Transform[0];
        Transform[] _driverBones = new Transform[0];
        Transform _rootBone;
        bool _disposed;

        internal Transform[] SourceBones { get { return _sourceBones; } }
        internal Transform RootBone { get { return _rootBone; } }

        CharacterRetarget() { }

        /// <summary>创建源骨架。space 是游戏身体网格所在的 Transform；alignment 是现有身体地标对齐。</summary>
        public static CharacterRetarget Create(Transform space, GltfLoader.Loaded skin,
            Transform[] driverBones, Matrix4x4[] driverBindposes, int[] jointToDriver,
            Matrix4x4 alignment, out string error)
        {
            error = "";
            if (space == null || skin == null || driverBones == null || driverBindposes == null || jointToDriver == null)
            { error = "the retarget input is null"; return null; }
            if (skin.JointNames.Length == 0 || skin.JointNodes.Length != skin.JointNames.Length
                || skin.InverseBind.Length != skin.JointNames.Length || skin.Nodes.Length == 0
                || jointToDriver.Length != skin.JointNames.Length)
            { error = "the GLB has no complete source node hierarchy"; return null; }

            var result = new CharacterRetarget { _space = space, _driverBones = driverBones };
            try
            {
                var flip = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
                var convert = flip * skin.VertexTransform;
                var inverseConvert = convert.inverse;
                var root = new GameObject("ModelKit_CharacterRetarget");
                result._root = root.transform;
                result._root.SetParent(space, false);
                SetLocalMatrix(result._root, alignment);

                var transforms = new Transform[skin.Nodes.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    var go = new GameObject("Source_" + skin.Nodes[i].Name);
                    transforms[i] = go.transform;
                }
                for (int i = 0; i < transforms.Length; i++)
                {
                    int parent = skin.Nodes[i].Parent;
                    if (parent >= transforms.Length || parent == i || parent < -1)
                        throw new Exception("invalid source node parent at " + skin.Nodes[i].Name);
                    transforms[i].SetParent(parent >= 0 ? transforms[parent] : result._root, false);
                    SetLocalMatrix(transforms[i], convert * skin.Nodes[i].LocalMatrix * inverseConvert);
                }

                var jointByNode = new Dictionary<int, int>();
                result._sourceBones = new Transform[skin.JointNames.Length];
                for (int j = 0; j < skin.JointNames.Length; j++)
                {
                    int node = skin.JointNodes[j];
                    if (node < 0 || node >= transforms.Length) throw new Exception("invalid source joint node: " + skin.JointNames[j]);
                    result._sourceBones[j] = transforms[node];
                    jointByNode[node] = j;
                    int driver = jointToDriver[j];
                    if (driver >= driverBones.Length || driver >= driverBindposes.Length)
                        throw new Exception("invalid driver bone index: " + driver);
                    if (string.Equals(skin.JointNames[j], "Root", StringComparison.Ordinal)
                        || string.Equals(CharacterRig.StripPrefix(skin.JointNames[j]), "Root", StringComparison.Ordinal))
                        result._rootBone = transforms[node];
                }
                if (result._rootBone == null && result._sourceBones.Length > 0) result._rootBone = result._sourceBones[0];

                var entries = new List<Entry>();
                for (int i = 0; i < transforms.Length; i++)
                {
                    int joint;
                    if (!jointByNode.TryGetValue(i, out joint)) continue;
                    int driver = jointToDriver[joint];
                    var sourceRest = alignment * convert * skin.InverseBind[joint].inverse * inverseConvert;
                    var entry = new Entry
                    {
                        Transform = transforms[i],
                        Node = i,
                        Joint = joint,
                        Driver = driver,
                        SourceRest = RotationOf(sourceRest),
                        DriverRest = driver >= 0 ? RotationOf(driverBindposes[driver].inverse) : Quaternion.identity,
                    };
                    entries.Add(entry);
                }
                entries.Sort((a, b) => Depth(skin.Nodes, a.Node).CompareTo(Depth(skin.Nodes, b.Node)));
                result._entries = entries.ToArray();
                // glTF 节点当前 TRS 不保证等于 inverseBindMatrices 描述的绑定姿势。
                // 先按父到子把每个关节校准到 bind pose，再开始只写旋转的运行时重定向。
                foreach (var e in result._entries)
                {
                    var restInRoot = convert * skin.InverseBind[e.Joint].inverse * inverseConvert;
                    var restWorld = result._root.localToWorldMatrix * restInRoot;
                    SetLocalMatrix(e.Transform, e.Transform.parent.worldToLocalMatrix * restWorld);
                }
                result.Apply();
                var driverBehaviour = result._root.gameObject.AddComponent<Driver>();
                driverBehaviour.Target = result;
                return result;
            }
            catch (Exception ex)
            {
                result.Dispose();
                error = ex.Message;
                return null;
            }
        }

        /// <summary>在 Animator 更新后调用。只写源关节旋转，不写位置或缩放。</summary>
        public void Apply()
        {
            if (_disposed || _space == null || _root == null) return;
            var worldToSpace = _space.worldToLocalMatrix;
            foreach (var e in _entries)
            {
                if (e.Driver < 0 || e.Transform == null || e.Transform.parent == null) continue;
                var driverBone = _driverBones[e.Driver];
                if (driverBone == null) continue;
                var currentDriver = worldToSpace * driverBone.localToWorldMatrix;
                var delta = RotationOf(currentDriver) * Quaternion.Inverse(e.DriverRest);
                var desiredInSpace = delta * e.SourceRest;
                var parentInSpace = worldToSpace * e.Transform.parent.localToWorldMatrix;
                e.Transform.localRotation = Quaternion.Inverse(RotationOf(parentInSpace)) * desiredInSpace;
            }
        }

        internal void Reassert(SkinnedMeshRenderer body)
        {
            if (_disposed || body == null) return;
            var bones = body.bones;
            bool same = bones != null && bones.Length == _sourceBones.Length;
            if (same)
                for (int i = 0; i < bones.Length; i++) if (bones[i] != _sourceBones[i]) { same = false; break; }
            if (!same) body.bones = _sourceBones;
            if (body.rootBone != _rootBone) body.rootBone = _rootBone;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_root != null)
            {
                try { UnityEngine.Object.Destroy(_root.gameObject); } catch { }
                _root = null;
            }
            _entries = new Entry[0];
            _sourceBones = new Transform[0];
            _driverBones = new Transform[0];
        }

        sealed class Driver : MonoBehaviour
        {
            public CharacterRetarget Target;
            bool _failed;
            void LateUpdate()
            {
                if (_failed || Target == null) return;
                try { Target.Apply(); }
                catch (Exception ex)
                {
                    _failed = true;
                    Debug.LogWarning("[CharacterRetarget] update failed: " + ex.Message);
                }
            }
        }

        static int Depth(GltfLoader.RigNode[] nodes, int index)
        {
            int depth = 0;
            var seen = new HashSet<int>();
            while (index >= 0 && index < nodes.Length && nodes[index].Parent >= 0)
            {
                if (!seen.Add(index)) throw new Exception("a cycle exists in the source node hierarchy");
                index = nodes[index].Parent;
                depth++;
            }
            return depth;
        }

        internal static Quaternion RotationOf(Matrix4x4 m)
        {
            Vector3 up = m.GetColumn(1);
            Vector3 forward = m.GetColumn(2);
            if (up.sqrMagnitude < 1e-10f || forward.sqrMagnitude < 1e-10f) return Quaternion.identity;
            return Quaternion.LookRotation(forward, up);
        }

        internal static void SetLocalMatrix(Transform target, Matrix4x4 matrix)
        {
            target.localPosition = matrix.GetColumn(3);
            target.localRotation = RotationOf(matrix);
            var scale = new Vector3(
                ((Vector3)matrix.GetColumn(0)).magnitude,
                ((Vector3)matrix.GetColumn(1)).magnitude,
                ((Vector3)matrix.GetColumn(2)).magnitude);
            if (matrix.determinant < 0f) scale.x = -scale.x;
            target.localScale = scale;
        }
    }
}
