﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Syroot.NintenTools.NSW.Bfres;
using Syroot.NintenTools.NSW.Bfres.Helpers;
using Syroot.NintenTools.NSW.Bfres.GFX;
using Switch_Toolbox.Library.IO;
using Switch_Toolbox.Library;
using Switch_Toolbox.Library.Rendering;
using Switch_Toolbox.Library.Forms;
using OpenTK;
using System.Windows.Forms;
using Bfres.Structs;
using Switch_Toolbox.Library.Animations;

namespace FirstPlugin
{
    public static class BfresSwitch
    {
        public static Model SetModel(FMDL fmdl)
        {
            Model model = new Model();
            model.Name = fmdl.Text;
            model.Path = "";

            model.Shapes = new List<Shape>();
            model.VertexBuffers = new List<VertexBuffer>();
            model.Materials = new List<Material>();
            model.UserData = new List<UserData>();
            model.Skeleton = new Skeleton();
            model.Skeleton = fmdl.Skeleton.node.Skeleton;
            model.ShapeDict = new ResDict();
            model.MaterialDict = new ResDict();
            model.UserDataDict = new ResDict();
            model.UserData = fmdl.Model.UserData;
            model.UserDataDict = fmdl.Model.UserDataDict;

            if (model.Skeleton.InverseModelMatrices == null)
                model.Skeleton.InverseModelMatrices = new List<Syroot.Maths.Matrix3x4>();
            if (model.Skeleton.MatrixToBoneList == null)
                model.Skeleton.MatrixToBoneList = new List<ushort>();

            foreach (var bone in fmdl.Skeleton.bones)
            {
                if (model.Skeleton.InverseModelMatrices.Count <= 0)
                    break;

                int inde = 0;
                foreach (var bn in model.Skeleton.Bones)
                {
                    if (bone.Text == bn.Name)
                    {
                        var mat = MatrixExenstion.GetMatrixInverted(bone);

                        if (bn.SmoothMatrixIndex > -1)
                            model.Skeleton.InverseModelMatrices[bn.SmoothMatrixIndex] = mat;
                    }
                    inde++;
                }
            }

            int i = 0;
            var duplicates = fmdl.shapes.GroupBy(c => c.Text).Where(g => g.Skip(1).Any()).SelectMany(c => c);
            foreach (var shape in duplicates)
                shape.Text += i++;

            foreach (FSHP shape in fmdl.shapes)
            {
                BFRES.CheckMissingTextures(shape);
                SetShape(shape, shape.Shape);

                model.Shapes.Add(shape.Shape);
                model.VertexBuffers.Add(shape.VertexBuffer);
                shape.Shape.VertexBufferIndex = (ushort)(model.VertexBuffers.Count - 1);

              //  BFRES.SetShaderAssignAttributes(shape.GetMaterial().shaderassign, shape);
            }
            foreach (FMAT mat in fmdl.materials.Values)
            {

                SetMaterial(mat, mat.Material);
                model.Materials.Add(mat.Material);
            }
            return model;
        }
        public static void ReadModel(FMDL model, Model mdl)
        {
            if (model == null)
                model = new FMDL();
            model.Text = mdl.Name;
            model.Skeleton = new FSKL(mdl.Skeleton);

            model.Nodes[2] = model.Skeleton.node;

            model.Model = mdl;
            foreach (Material mat in mdl.Materials)
            {
                FMAT FMAT = new FMAT();
                FMAT.Text = mat.Name;
                FMAT.ReadMaterial(mat);
                model.Nodes[1].Nodes.Add(FMAT);
                model.materials.Add(FMAT.Text, FMAT);
            }
            foreach (Shape shp in mdl.Shapes)
            {
                VertexBuffer vertexBuffer = mdl.VertexBuffers[shp.VertexBufferIndex];
                FSHP mesh = new FSHP();
                ReadShapesVertices(mesh, shp, vertexBuffer, model);
                mesh.MaterialIndex = shp.MaterialIndex;

                model.Nodes[0].Nodes.Add(mesh);
                model.shapes.Add(mesh);
            }
        }
        public static Shape SaveShape(FSHP fshp)
        {
            Shape Shape = new Shape();
            Shape.VertexSkinCount = (byte)fshp.VertexSkinCount;
            Shape.Flags = ShapeFlags.HasVertexBuffer;
            Shape.BoneIndex = (ushort)fshp.BoneIndex;
            Shape.MaterialIndex = (ushort)fshp.MaterialIndex;
            Shape.VertexBufferIndex = (ushort)fshp.VertexBufferIndex;
            Shape.KeyShapes = new List<KeyShape>();
            Shape.KeyShapeDict = new ResDict();
            Shape.Name = fshp.Text;
            Shape.SkinBoneIndices = fshp.GetIndices();
            Shape.TargetAttribCount = (byte)fshp.TargetAttribCount;
            Shape.SkinBoneIndices = fshp.BoneIndices;
            Shape.SubMeshBoundings = new List<Bounding>();
            Shape.RadiusArray = new List<float>();
            Shape.RadiusArray = fshp.boundingRadius;

            Shape.Meshes = new List<Mesh>();

            foreach (FSHP.BoundingBox box in fshp.boundingBoxes)
            {
                Bounding bnd = new Bounding();
                bnd.Center = new Syroot.Maths.Vector3F(box.Center.X, box.Center.Y, box.Center.Z);
                bnd.Extent = new Syroot.Maths.Vector3F(box.Extend.X, box.Extend.Y, box.Extend.Z);
                Shape.SubMeshBoundings.Add(bnd);
            }

            foreach (FSHP.LOD_Mesh mesh in fshp.lodMeshes)
            {
                Mesh msh = new Mesh();
                msh.MemoryPool = new MemoryPool();
                msh.SubMeshes = new List<SubMesh>();
                msh.PrimitiveType = (PrimitiveType)mesh.PrimitiveType;
                msh.FirstVertex = mesh.FirstVertex;

                foreach (FSHP.LOD_Mesh.SubMesh sub in mesh.subMeshes)
                {
                    SubMesh subMesh = new SubMesh();
                    subMesh.Offset = sub.offset;
                    subMesh.Count = (uint)mesh.faces.Count;
                    msh.SubMeshes.Add(subMesh);
                }

                IList<uint> faceList = new List<uint>();
                foreach (int f in mesh.faces)
                {
                    faceList.Add((uint)f);
                }
                if (faceList.Count > 65000)
                {
                    MessageBox.Show($"Warning! Your poly count for a single mesh {fshp.Text} is pretty high! ({faceList.Count})." +
                        $" You may want to split this!");
                    msh.SetIndices(faceList, IndexFormat.UInt32);
                }
                else
                    msh.SetIndices(faceList, IndexFormat.UInt16);

                Shape.Meshes.Add(msh);
                break;
            }
            return Shape;
        }
        public static void ReadShapesVertices(FSHP fshp, Shape shp, VertexBuffer vertexBuffer, FMDL model)
        {
            fshp.boundingBoxes.Clear();
            fshp.boundingRadius.Clear();
            fshp.BoneIndices.Clear();

            foreach (Bounding bnd in shp.SubMeshBoundings)
            {
                FSHP. BoundingBox box = new FSHP.BoundingBox();
                box.Center = new Vector3(bnd.Center.X, bnd.Center.Y, bnd.Center.Z);
                box.Extend = new Vector3(bnd.Extent.X, bnd.Extent.Y, bnd.Extent.Z);
                fshp.boundingBoxes.Add(box);
            }
            foreach (float rad in shp.RadiusArray)
            {
                fshp.boundingRadius.Add(rad);
            }

            fshp.VertexBufferIndex = shp.VertexBufferIndex;
            fshp.Shape = shp;
            fshp.VertexBuffer = vertexBuffer;
            fshp.VertexSkinCount = shp.VertexSkinCount;
            fshp.BoneIndex = shp.BoneIndex;
            fshp.Text = shp.Name;
            fshp.TargetAttribCount = shp.TargetAttribCount;
            fshp.MaterialIndex = shp.MaterialIndex;

            if (shp.SkinBoneIndices != null)
            {
                foreach (ushort bn in shp.SkinBoneIndices)
                    fshp.BoneIndices.Add(bn);
            }

            ReadMeshes(fshp, shp);
            ReadVertexBuffer(fshp, vertexBuffer, model);
        }
        private static void ReadMeshes(FSHP fshp, Shape shp)
        {
            fshp.lodMeshes.Clear();

            foreach (Mesh msh in shp.Meshes)
            {
                uint FaceCount = msh.IndexCount;
                uint[] indicesArray = msh.GetIndices().ToArray();

                FSHP.LOD_Mesh lod = new FSHP.LOD_Mesh();
                foreach (SubMesh subMsh in msh.SubMeshes)
                {
                    FSHP.LOD_Mesh.SubMesh sub = new FSHP.LOD_Mesh.SubMesh();
                    sub.size = subMsh.Count;
                    sub.offset = subMsh.Offset;
                    lod.subMeshes.Add(sub);
                }
                lod.IndexFormat = (STIndexFormat)msh.IndexFormat;
                lod.PrimitiveType = (STPolygonType)msh.PrimitiveType;
                lod.FirstVertex = msh.FirstVertex;

                for (int face = 0; face < FaceCount; face++)
                    lod.faces.Add((int)indicesArray[face] + (int)msh.FirstVertex);

                fshp.lodMeshes.Add(lod);
            }
        }
        private static void ReadVertexBuffer(FSHP fshp, VertexBuffer vtx, FMDL model)
        {
            fshp.vertices.Clear();
            fshp.vertexAttributes.Clear();

            //Create a buffer instance which stores all the buffer data
            VertexBufferHelper helper = new VertexBufferHelper(vtx);

            //Set each array first from the lib if exist. Then add the data all in one loop
            Syroot.Maths.Vector4F[] vec4Positions = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4Normals = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4uv0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4uv1 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4uv2 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4c0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4t0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4b0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4w0 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4i0 = new Syroot.Maths.Vector4F[0];

            //For shape morphing
            Syroot.Maths.Vector4F[] vec4Positions1 = new Syroot.Maths.Vector4F[0];
            Syroot.Maths.Vector4F[] vec4Positions2 = new Syroot.Maths.Vector4F[0];

            foreach (VertexAttrib att in vtx.Attributes)
            {
                FSHP.VertexAttribute attr = new FSHP.VertexAttribute();
                attr.Name = att.Name;
                attr.Format = att.Format;

                if (att.Name == "_p0")
                    vec4Positions = AttributeData(att, helper, "_p0");
                if (att.Name == "_n0")
                    vec4Normals = AttributeData(att, helper, "_n0");
                if (att.Name == "_u0")
                    vec4uv0 = AttributeData(att, helper, "_u0");
                if (att.Name == "_u1")
                    vec4uv1 = AttributeData(att, helper, "_u1");
                if (att.Name == "_u2")
                    vec4uv2 = AttributeData(att, helper, "_u2");
                if (att.Name == "_c0")
                    vec4c0 = AttributeData(att, helper, "_c0");
                if (att.Name == "_t0")
                    vec4t0 = AttributeData(att, helper, "_t0");
                if (att.Name == "_b0")
                    vec4b0 = AttributeData(att, helper, "_b0");
                if (att.Name == "_w0")
                    vec4w0 = AttributeData(att, helper, "_w0");
                if (att.Name == "_i0")
                    vec4i0 = AttributeData(att, helper, "_i0");

                if (att.Name == "_p1")
                    vec4Positions1 = AttributeData(att, helper, "_p1");
                if (att.Name == "_p2")
                    vec4Positions2 = AttributeData(att, helper, "_p2");

                fshp.vertexAttributes.Add(attr);
            }
            for (int i = 0; i < vec4Positions.Length; i++)
            {
                Vertex v = new Vertex();
                if (vec4Positions.Length > 0)
                    v.pos = new Vector3(vec4Positions[i].X, vec4Positions[i].Y, vec4Positions[i].Z);
                if (vec4Positions1.Length > 0)
                    v.pos1 = new Vector3(vec4Positions1[i].X, vec4Positions1[i].Y, vec4Positions1[i].Z);
                if (vec4Positions2.Length > 0)
                    v.pos2 = new Vector3(vec4Positions2[i].X, vec4Positions2[i].Y, vec4Positions2[i].Z);
                if (vec4Normals.Length > 0)
                    v.nrm = new Vector3(vec4Normals[i].X, vec4Normals[i].Y, vec4Normals[i].Z);
                if (vec4uv0.Length > 0)
                    v.uv0 = new Vector2(vec4uv0[i].X, vec4uv0[i].Y);
                if (vec4uv1.Length > 0)
                    v.uv1 = new Vector2(vec4uv1[i].X, vec4uv1[i].Y);
                if (vec4uv2.Length > 0)
                    v.uv2 = new Vector2(vec4uv2[i].X, vec4uv2[i].Y);
                if (vec4w0.Length > 0)
                {
                    v.boneWeights.Add(vec4w0[i].X);
                    v.boneWeights.Add(vec4w0[i].Y);
                    v.boneWeights.Add(vec4w0[i].Z);
                    v.boneWeights.Add(vec4w0[i].W);
                }
                if (vec4i0.Length > 0)
                {
                    v.boneIds.Add((int)vec4i0[i].X);
                    v.boneIds.Add((int)vec4i0[i].Y);
                    v.boneIds.Add((int)vec4i0[i].Z);
                    v.boneIds.Add((int)vec4i0[i].W);

                }

                if (vec4t0.Length > 0)
                    v.tan = new Vector4(vec4t0[i].X, vec4t0[i].Y, vec4t0[i].Z, vec4t0[i].W);
                if (vec4b0.Length > 0)
                    v.bitan = new Vector4(vec4b0[i].X, vec4b0[i].Y, vec4b0[i].Z, vec4b0[i].W);
                if (vec4c0.Length > 0)
                    v.col = new Vector4(vec4c0[i].X, vec4c0[i].Y, vec4c0[i].Z, vec4c0[i].W);

                if (fshp.VertexSkinCount == 1)
                {
                    Matrix4 sb = model.Skeleton.bones[model.Skeleton.Node_Array[v.boneIds[0]]].Transform;
                    v.pos = Vector3.TransformPosition(v.pos, sb);
                    v.nrm = Vector3.TransformNormal(v.nrm, sb);
                }
                if (fshp.VertexSkinCount == 0)
                {
                    Matrix4 NoBindFix = model.Skeleton.bones[fshp.BoneIndex].Transform;
                    v.pos = Vector3.TransformPosition(v.pos, NoBindFix);
                    v.nrm = Vector3.TransformNormal(v.nrm, NoBindFix);
                }
                fshp.vertices.Add(v);
            }
        }
        private static Syroot.Maths.Vector4F[] AttributeData(VertexAttrib att, VertexBufferHelper helper, string attName)
        {
            VertexBufferHelperAttrib attd = helper[attName];
            return attd.Data;
        }
        public static SkeletalAnim SetSkeletalAniamtion(FSKA anim)
        {
            SkeletalAnim animation = new SkeletalAnim();

            animation.Name = anim.Text;
            animation.FrameCount = anim.FrameCount;
            animation.FlagsAnimSettings = SkeletalAnimFlags.Looping;
            animation.FlagsRotate = SkeletalAnimFlagsRotate.EulerXYZ;
            animation.FlagsScale = SkeletalAnimFlagsScale.Maya;
            animation.BindIndices = new ushort[anim.Bones.Count];
            animation.BindSkeleton = new Skeleton();
            animation.BakedSize = 0;
            animation.BoneAnims = new List<BoneAnim>();
            animation.UserDataDict = new ResDict();
            animation.UserDatas = new List<UserData>();

            foreach (var bone in anim.Bones)
                animation.BoneAnims.Add(createBoneAnim(bone, anim));

            return animation;
        }
        private static BoneAnim createBoneAnim(Animation.KeyNode bone, FSKA anim)
        {
            BoneAnim boneAnim = new BoneAnim();
            boneAnim.Name = bone.Name;
            var posx = bone.XPOS.GetValue(0);
            var posy = bone.YPOS.GetValue(0);
            var posz = bone.ZPOS.GetValue(0);
            var scax = bone.XSCA.GetValue(0);
            var scay = bone.YSCA.GetValue(0);
            var scaz = bone.ZSCA.GetValue(0);
            var rotx = bone.XROT.GetValue(0);
            var roty = bone.YROT.GetValue(0);
            var rotz = bone.ZROT.GetValue(0);
            var rotw = bone.WROT.GetValue(0);

            BoneAnimData boneBaseData = new BoneAnimData();
            boneBaseData.Translate = new Syroot.Maths.Vector3F(posx, posy, posz);
            boneBaseData.Scale = new Syroot.Maths.Vector3F(scax, scay, scaz);
            boneBaseData.Rotate = new Syroot.Maths.Vector4F(rotx, roty, rotz, rotw);
            boneAnim.BaseData = boneBaseData;
            boneAnim.BeginBaseTranslate = 0;
            boneAnim.BeginRotate = 0;
            boneAnim.BeginTranslate = 0;
            boneAnim.Curves = new List<AnimCurve>();
            boneAnim.FlagsBase = BoneAnimFlagsBase.Translate | BoneAnimFlagsBase.Scale | BoneAnimFlagsBase.Rotate;
            boneAnim.FlagsTransform = BoneAnimFlagsTransform.Identity;

            if (bone.XPOS.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.TranslateX;
                boneAnim.Curves.Add(SetAnimationCurve(bone.XPOS));
            }
            if (bone.YPOS.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.TranslateY;
                boneAnim.Curves.Add(SetAnimationCurve(bone.YPOS));
            }
            if (bone.ZPOS.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.TranslateZ;
                boneAnim.Curves.Add(SetAnimationCurve(bone.ZPOS));
            }
            if (bone.XSCA.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.ScaleX;
                boneAnim.Curves.Add(SetAnimationCurve(bone.XSCA));
            }
            if (bone.YSCA.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.ScaleY;
                boneAnim.Curves.Add(SetAnimationCurve(bone.YSCA));
            }
            if (bone.ZSCA.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.ScaleZ;
                boneAnim.Curves.Add(SetAnimationCurve(bone.ZSCA));
            }
            if (bone.XROT.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateX;
                boneAnim.Curves.Add(SetAnimationCurve(bone.XROT));
            }
            if (bone.YROT.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateY;
                boneAnim.Curves.Add(SetAnimationCurve(bone.YROT));
            }
            if (bone.ZROT.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateZ;
                boneAnim.Curves.Add(SetAnimationCurve(bone.ZROT));
            }
            if (bone.WROT.HasAnimation())
            {
                boneAnim.FlagsCurve |= BoneAnimFlagsCurve.RotateW;
                boneAnim.Curves.Add(SetAnimationCurve(bone.WROT));
            }

            return boneAnim;
        }
        private static AnimCurve SetAnimationCurve(Animation.KeyGroup keyGroup)
        {
            AnimCurve curve = new AnimCurve();
            curve.Frames = new float[(int)keyGroup.Keys.Count];
            curve.FrameType = AnimCurveFrameType.Single;
            curve.KeyType = AnimCurveKeyType.Single;
            curve.EndFrame = keyGroup.FrameCount;
            curve.AnimDataOffset = 0;
            curve.Delta = 0;
            curve.Scale = 1;
            curve.StartFrame = 0;
            curve.Offset = 0;

            var frame = keyGroup.GetKeyFrame(0);
            int valuesLength = 1;
            if (frame.InterType == InterpolationType.HERMITE)
            {
                curve.CurveType = AnimCurveType.Cubic;
                curve.Keys = new float[keyGroup.Keys.Count, 4];
                for (int k = 0; k < keyGroup.Keys.Count; k++)
                {
                    float value = keyGroup.GetValue(keyGroup.Keys[k].Frame);
                    curve.Keys[k, 0] = value;
                    curve.Keys[k, 1] = 0;
                    curve.Keys[k, 2] = 0;
                    curve.Keys[k, 3] = 0;

                    curve.Frames[k] = keyGroup.Keys[k].Frame;
                }
            }
            if (frame.InterType == InterpolationType.LINEAR)
            {
                curve.CurveType = AnimCurveType.Linear;
                curve.Keys = new float[keyGroup.Keys.Count, 2];
            }
            if (frame.InterType == InterpolationType.STEP)
            {
                curve.CurveType = AnimCurveType.StepInt;
                curve.Keys = new float[keyGroup.Keys.Count, 1];
            }
            if (frame.InterType == InterpolationType.STEPBOOL)
            {
                curve.CurveType = AnimCurveType.StepBool;
                curve.Keys = new float[keyGroup.Keys.Count, 1];
            }
            return curve;
        }
        public static void ReadSkeleton(this TreeNodeCustom skl, Skeleton skeleton, FSKL RenderableSkeleton)
        {
            if (skeleton.MatrixToBoneList == null)
                skeleton.MatrixToBoneList = new List<ushort>();

            RenderableSkeleton.Node_Array = new int[skeleton.MatrixToBoneList.Count];
            int nodes = 0;
            foreach (ushort node in skeleton.MatrixToBoneList)
            {
                RenderableSkeleton.Node_Array[nodes] = node;
                nodes++;
            }

            foreach (Bone bone in skeleton.Bones)
            {
                BfresBone STBone = new BfresBone(RenderableSkeleton);
                ReadBone(STBone, bone);
                RenderableSkeleton.bones.Add(STBone);
            }
            foreach (var bone in RenderableSkeleton.bones)
                if (bone.Parent == null)
                {
                    skl.Nodes.Add(bone);
                }

            RenderableSkeleton.update();
            RenderableSkeleton.reset();


        }
        public static void ReadBone(this BfresBone bone, Bone bn, bool SetParent = false)
        {
            bone.Bone = bn;
            bone.FlagVisible = bn.Flags.HasFlag(BoneFlags.Visible);
            bone.Text = bn.Name;
            bone.RigidMatrixIndex = bn.RigidMatrixIndex;
            bone.SmoothMatrixIndex = bn.SmoothMatrixIndex;
            bone.BillboardIndex = bn.BillboardIndex;
            if (!SetParent)
                bone.parentIndex = bn.ParentIndex;

            bone.scale = new float[3];
            bone.rotation = new float[4];
            bone.position = new float[3];
            if (bn.FlagsRotation == BoneFlagsRotation.Quaternion)
                bone.RotationType = STBone.BoneRotationType.Quaternion;
            else
                bone.RotationType = STBone.BoneRotationType.Euler;
            bone.scale[0] = bn.Scale.X;
            bone.scale[1] = bn.Scale.Y;
            bone.scale[2] = bn.Scale.Z;
            bone.rotation[0] = bn.Rotation.X;
            bone.rotation[1] = bn.Rotation.Y;
            bone.rotation[2] = bn.Rotation.Z;
            bone.rotation[3] = bn.Rotation.W;
            bone.position[0] = bn.Position.X;
            bone.position[1] = bn.Position.Y;
            bone.position[2] = bn.Position.Z;
        }

        public static void SaveSkeleton(FSKL fskl)
        {
            fskl.node.Skeleton.Bones.Clear();
            fskl.node.Skeleton.MatrixToBoneList = new List<ushort>();
            fskl.node.Skeleton.InverseModelMatrices = new List<Syroot.Maths.Matrix3x4>();

            fskl.node.Nodes.Clear();

            ushort SmoothIndex = 0;
            foreach (STBone genericBone in fskl.bones)
            {
                genericBone.BillboardIndex = ushort.MaxValue;

                BfresBone bn = new BfresBone(fskl);
                bn.CloneBaseInstance(genericBone);

                if (bn.Bone == null)
                    bn.Bone = new Bone();

                bn.Bone.Position = new Syroot.Maths.Vector3F(bn.position[0], bn.position[1], bn.position[2]);
                bn.Bone.Scale = new Syroot.Maths.Vector3F(bn.scale[0], bn.scale[1], bn.scale[2]);
                bn.Bone.Rotation = new Syroot.Maths.Vector4F(bn.rotation[0], bn.rotation[1], bn.rotation[2], bn.rotation[3]);
                bn.Bone.Name = bn.Text;
                bn.Bone.Flags = bn.FlagVisible ? BoneFlags.Visible : 0;
                bn.Bone.ParentIndex = (ushort)bn.parentIndex;
                bn.Bone.SmoothMatrixIndex = bn.SmoothMatrixIndex;
                bn.Bone.RigidMatrixIndex = bn.RigidMatrixIndex;

                bn.SetTransforms();

                fskl.node.Skeleton.Bones.Add(bn.Bone);

                if (bn.Parent == null)
                    fskl.node.Nodes.Add(bn);

                if (bn.SmoothMatrixIndex != short.MaxValue)
                    fskl.node.Skeleton.MatrixToBoneList.Add(SmoothIndex++);

                fskl.node.Skeleton.InverseModelMatrices.Add(Syroot.Maths.Matrix3x4.Zero);
            }

            fskl.Node_Array = new int[fskl.node.Skeleton.MatrixToBoneList.Count];
            int nodes = 0;
            foreach (ushort node in fskl.node.Skeleton.MatrixToBoneList)
            {
                fskl.Node_Array[nodes] = node;
                nodes++;
            }
        }

        public static void SetShape(this FSHP s, Shape shp)
        {
            shp.Name = s.Text;
            shp.MaterialIndex = (ushort)s.MaterialIndex;
            shp.BoneIndex = (ushort)s.BoneIndex;
        }
        public static void CreateNewMaterial(string Name)
        {
            FMAT mat = new FMAT();
            mat.Text = Name;
            mat.Material = new Material();

            SetMaterial(mat, mat.Material);
        }
        public static void SetMaterial(this FMAT m, Material mat)
        {
            mat.Name = m.Text;

            if (m.Enabled)
                mat.Flags = MaterialFlags.Visible;
            else
                mat.Flags = MaterialFlags.None;

            if (mat.ShaderParamData == null)
                mat.ShaderParamData = new byte[0];

            byte[] ParamData = WriteShaderParams(m, mat);

            if (ParamData.Length != mat.ShaderParamData.Length)
                throw new Exception("Param size mis match!");
            else
                mat.ShaderParamData = ParamData;

            WriteRenderInfo(m, mat);
            WriteTextureRefs(m, mat);
            WriteShaderAssign(m.shaderassign, mat);
        }
        public static void ReadMaterial(this FMAT m, Material mat)
        {
            if (mat.Flags == MaterialFlags.Visible)
                m.Enabled = true;
            else
                m.Enabled = false;

            m.ReadRenderInfo(mat);
            m.ReadShaderAssign(mat);
            m.SetActiveGame();
            m.ReadShaderParams(mat);
            m.Material = mat;
            m.ReadTextureRefs(mat);

            if (Runtime.activeGame == Runtime.ActiveGame.KSA)
                KsaShader.LoadRenderInfo(m, m.renderinfo);
        }
        public static void ReadTextureRefs(this FMAT m, Material mat)
        {
            m.TextureMaps.Clear();

            int AlbedoCount = 0;
            int id = 0;
            string TextureName = "";
            if (mat.TextureRefs == null)
                mat.TextureRefs = new List<string>();

            int textureUnit = 1;
            foreach (string tex in mat.TextureRefs)
            {
                TextureName = tex;

                MatTexture texture = new MatTexture();
                texture.switchSampler = mat.Samplers[id];

                texture.wrapModeS = (int)mat.Samplers[id].WrapModeU;
                texture.wrapModeT = (int)mat.Samplers[id].WrapModeV;
                texture.wrapModeW = (int)mat.Samplers[id].WrapModeW;
                texture.SamplerName = mat.SamplerDict.GetKey(id);

                Console.WriteLine("Filter " + mat.Samplers[id].FilterMode);

                bool IsAlbedo = Misc.HackyTextureList.Any(TextureName.Contains);


                if (Runtime.activeGame == Runtime.ActiveGame.MK8D)
                {
                    if (texture.SamplerName == "_a0")
                    {
                        if (AlbedoCount == 0)
                        {
                            m.HasDiffuseMap = true;
                            AlbedoCount++;
                            texture.Type = MatTexture.TextureType.Diffuse;
                        }
                    }
                    if (texture.SamplerName == "_n0")
                    {
                        m.HasNormalMap = true;
                        texture.Type = MatTexture.TextureType.Normal;
                    }
                    if (texture.SamplerName == "_e0")
                    {
                        m.HasEmissionMap = true;
                        texture.Type = MatTexture.TextureType.Emission;
                    }
                    if (texture.SamplerName == "_s0")
                    {
                        m.HasSpecularMap = true;
                        texture.Type = MatTexture.TextureType.Specular;
                    }
                    if (texture.SamplerName == "_x0")
                    {
                        m.HasSphereMap = true;
                        texture.Type = MatTexture.TextureType.SphereMap;
                    }
                    if (texture.SamplerName == "_b0")
                    {
                        m.HasShadowMap = true;
                        texture.Type = MatTexture.TextureType.Shadow;
                    }
                    if (texture.SamplerName == "_b1")
                    {
                        m.HasLightMap = true;
                        texture.Type = MatTexture.TextureType.Light;
                    }
                }
                else if (Runtime.activeGame == Runtime.ActiveGame.SMO)
                {
                    if (texture.SamplerName == "_a0")
                    {
                        if (AlbedoCount == 0)
                        {
                            m.HasDiffuseMap = true;
                            AlbedoCount++;
                            texture.Type = MatTexture.TextureType.Diffuse;
                        }
                    }
                    if (texture.SamplerName == "_n0")
                    {
                        m.HasNormalMap = true;
                        texture.Type = MatTexture.TextureType.Normal;
                    }
                    if (texture.SamplerName == "_e0")
                    {
                        m.HasEmissionMap = true;
                        texture.Type = MatTexture.TextureType.Emission;
                    }
                    if (TextureName.Contains("mtl"))
                    {
                        m.HasMetalnessMap = true;
                        texture.Type = MatTexture.TextureType.Metalness;
                    }
                    else if (TextureName.Contains("rgh"))
                    {
                        texture.Type = MatTexture.TextureType.Roughness;
                        m.HasRoughnessMap = true;
                    }
                    else if (TextureName.Contains("sss"))
                    {

                        texture.Type = MatTexture.TextureType.SubSurfaceScattering;
                        m.HasSubSurfaceScatteringMap = true;
                    }
                }
                else
                {
                    //This works decently for now. I tried samplers but Kirby Star Allies doesn't map with samplers properly? 
                    if (IsAlbedo)
                    {
                        if (AlbedoCount == 0)
                        {
                            m.HasDiffuseMap = true;
                            AlbedoCount++;
                            texture.Type = MatTexture.TextureType.Diffuse;
                        }
                        if (AlbedoCount == 1)
                        {
                            //   poly.material.HasDiffuseLayer = true;
                            //    texture.hash = 19;
                            //   texture.Type = MatTexture.TextureType.DiffuseLayer2;

                        }
                    }

                    else if (TextureName.Contains("Nrm") || TextureName.Contains("Norm") || TextureName.Contains("norm") || TextureName.Contains("nrm"))
                    {
                        m.HasNormalMap = true;
                        texture.Type = MatTexture.TextureType.Normal;
                    }
                    else if (TextureName.Contains("Emm"))
                    {
                        m.HasEmissionMap = true;
                        texture.Type = MatTexture.TextureType.Emission;
                    }
                    else if (TextureName.Contains("Spm"))
                    {
                        m.HasSpecularMap = true;
                        texture.Type = MatTexture.TextureType.Specular;
                    }
                    else if (TextureName.Contains("b00"))
                    {
                        m.HasShadowMap = true;
                        texture.Type = MatTexture.TextureType.Shadow;
                    }
                    else if (TextureName.Contains("Moc") || TextureName.Contains("AO"))
                    {
                        m.HasAmbientOcclusionMap = true;
                        texture.Type = MatTexture.TextureType.AO;
                    }
                    else if (TextureName.Contains("b01"))
                    {
                        m.HasLightMap = true;
                        texture.Type = MatTexture.TextureType.Light;
                    }
                    else if (TextureName.Contains("MRA")) //Metalness, Roughness, and Cavity Map in one
                    {
                        m.HasMRA = true;
                        texture.Type = MatTexture.TextureType.MRA;
                    }
                    else if (TextureName.Contains("mtl"))
                    {
                        m.HasMetalnessMap = true;
                        texture.Type = MatTexture.TextureType.Metalness;
                    }
                    else if (TextureName.Contains("rgh"))
                    {
                        texture.Type = MatTexture.TextureType.Roughness;
                        m.HasRoughnessMap = true;
                    }
                    else if (TextureName.Contains("sss"))
                    {

                        texture.Type = MatTexture.TextureType.SubSurfaceScattering;
                        m.HasSubSurfaceScatteringMap = true;
                    }
                }
                texture.Name = TextureName;

                texture.textureUnit = textureUnit++;

                m.TextureMaps.Add(texture);

                id++;
            }
        }
        public static void ReadShaderParams(this FMAT m, Material mat)
        {
            m.matparam.Clear();

            if (mat.ShaderParamData == null)
                return;

            using (FileReader reader = new FileReader(new System.IO.MemoryStream(mat.ShaderParamData)))
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                foreach (ShaderParam param in mat.ShaderParams)
                {
                    BfresShaderParam shaderParam = new BfresShaderParam();
                    shaderParam.Type = param.Type;
                    shaderParam.Name = param.Name;

                    reader.Seek(param.DataOffset, System.IO.SeekOrigin.Begin);
                    shaderParam.ReadValue(reader, (int)param.DataSize);

                    m.matparam.Add(param.Name, shaderParam);
                }
                reader.Close();
            }
        }

        public static byte[] WriteShaderParams(this FMAT m, Material mat)
        {
            mat.ShaderParams = new List<ShaderParam>();

            System.IO.MemoryStream mem = new System.IO.MemoryStream();
            using (FileWriter writer = new FileWriter(mem))
            {
                uint Offset = 0;
                int index = 0;
                writer.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                foreach (BfresShaderParam shaderParam in m.matparam.Values)
                {
                    ShaderParam param = new ShaderParam();
                    param.Name = shaderParam.Name;
                    param.Type = shaderParam.Type;
                    param.DataOffset = (ushort)Offset;
                    param.DependedIndex = (ushort)index;
                    param.DependIndex = (ushort)index;

                    writer.Seek(param.DataOffset, System.IO.SeekOrigin.Begin);
                    shaderParam.WriteValue(writer);

                    Offset += param.DataSize;
                    mat.ShaderParams.Add(param);
                    index++;
                }
                writer.Close();
            }
            return mem.ToArray();
        }
        public static void ReadRenderInfo(this FMAT m, Material mat)
        {
            m.renderinfo.Clear();

            foreach (RenderInfo rnd in mat.RenderInfos)
            {
                BfresRenderInfo r = new BfresRenderInfo();
                r.Name = rnd.Name;
                r.Type = rnd.Type;
                switch (rnd.Type)
                {
                    case RenderInfoType.Int32: r.ValueInt = rnd.GetValueInt32s(); break;
                    case RenderInfoType.Single: r.ValueFloat = rnd.GetValueSingles(); break;
                    case RenderInfoType.String: r.ValueString = rnd.GetValueStrings(); break;
                }
                m.renderinfo.Add(r);
            }
        }
        public static void WriteTextureRefs(this FMAT m, Material mat)
        {
            mat.TextureRefs = new List<string>();
            mat.TextureRefs.Clear();

            foreach (var textu in m.TextureMaps)
                mat.TextureRefs.Add(textu.Name);
            
        }
        public static void WriteRenderInfo(this FMAT m, Material mat)
        {
            if (mat.RenderInfos == null)
                mat.RenderInfos = new List<RenderInfo>();

            mat.RenderInfos.Clear();
            foreach (BfresRenderInfo rnd in m.renderinfo)
            {
                RenderInfo r = new RenderInfo();
                r.Name = rnd.Name;
                switch (rnd.Type)
                {
                    case RenderInfoType.Int32: r.SetValue(rnd.ValueInt); break;
                    case RenderInfoType.Single: r.SetValue(rnd.ValueFloat); break;
                    case RenderInfoType.String: r.SetValue(rnd.ValueString); break;
                }
                mat.RenderInfos.Add(r);
            }
        }
        public static void ReadShaderAssign(this FMAT m, Material mat)
        {
            m.shaderassign = new FMAT.ShaderAssign();

            if (mat.ShaderAssign == null)
                mat.ShaderAssign = new ShaderAssign();
            if (mat.ShaderAssign.ShaderOptions == null)
                mat.ShaderAssign.ShaderOptions = new List<string>();
            if (mat.ShaderAssign.AttribAssigns == null)
                mat.ShaderAssign.AttribAssigns = new List<string>();
            if (mat.ShaderAssign.SamplerAssigns == null)
                mat.ShaderAssign.SamplerAssigns = new List<string>();

            m.shaderassign.options.Clear();
            m.shaderassign.samplers.Clear();
            m.shaderassign.attributes.Clear();

            m.shaderassign = new FMAT.ShaderAssign();
            m.shaderassign.ShaderArchive = mat.ShaderAssign.ShaderArchiveName;
            m.shaderassign.ShaderModel = mat.ShaderAssign.ShadingModelName;

            for (int op = 0; op < mat.ShaderAssign.ShaderOptions.Count; op++)
                m.shaderassign.options.Add(mat.ShaderAssign.ShaderOptionDict.GetKey(op), mat.ShaderAssign.ShaderOptions[op]);

            if (mat.ShaderAssign.SamplerAssigns != null)
            {
                for (int op = 0; op < mat.ShaderAssign.SamplerAssigns.Count; op++)
                    m.shaderassign.samplers.Add(mat.ShaderAssign.SamplerAssignDict.GetKey(op), mat.ShaderAssign.SamplerAssigns[op]);
            }
            if (mat.ShaderAssign.AttribAssigns != null)
            {
                for (int op = 0; op < mat.ShaderAssign.AttribAssigns.Count; op++)
                    m.shaderassign.attributes.Add(mat.ShaderAssign.AttribAssignDict.GetKey(op), mat.ShaderAssign.AttribAssigns[op]);
            }

     
            
       
            
        }
        public static void WriteShaderAssign(this FMAT.ShaderAssign shd, Material mat)
        {
            mat.ShaderAssign = new ShaderAssign();
            mat.ShaderAssign.ShaderOptionDict = new ResDict();
            mat.ShaderAssign.SamplerAssignDict = new ResDict();
            mat.ShaderAssign.AttribAssignDict = new ResDict();
            mat.ShaderAssign.ShaderOptions = new List<string>();
            mat.ShaderAssign.AttribAssigns = new List<string>();
            mat.ShaderAssign.SamplerAssigns = new List<string>();

            mat.ShaderAssign.ShaderArchiveName = shd.ShaderArchive;
            mat.ShaderAssign.ShadingModelName = shd.ShaderModel;

            foreach (var option in shd.options)
            {
                mat.ShaderAssign.ShaderOptionDict.Add(option.Key);
                mat.ShaderAssign.ShaderOptions.Add(option.Value);
            }
            foreach (var samp in shd.samplers)
            {
                mat.ShaderAssign.SamplerAssignDict.Add(samp.Key);
                mat.ShaderAssign.SamplerAssigns.Add(samp.Value);
            }
            foreach (var att in shd.attributes)
            {
                mat.ShaderAssign.AttribAssignDict.Add(att.Key);
                mat.ShaderAssign.AttribAssigns.Add(att.Value);
            }
        }
        public static void WriteExternalFiles(ResFile resFile, TreeNode EditorRoot)
        {
            resFile.ExternalFiles.Clear();
            if (EditorRoot.Nodes.ContainsKey("EXT"))
            {
                foreach (TreeNode node in EditorRoot.Nodes["EXT"].Nodes)
                {
                    ExternalFile ext = new ExternalFile();
                    if (node is BNTX)
                        ext.Data = ((BNTX)node).Save();
                    else if (node is IFileFormat && ((IFileFormat)node).CanSave)
                        ext.Data = ((IFileFormat)node).Save();
                    else
                        ext.Data = ((ExternalFileData)node).Data;
                    
                    resFile.ExternalFiles.Add(ext);
                }
            }
        }
    }
}
