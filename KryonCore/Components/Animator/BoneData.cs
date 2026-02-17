using OpenTK.Mathematics;
using System.Collections.Generic;

namespace KrayonCore.Animation
{
    public struct BoneInfo
    {
        public int Id;
        public Matrix4 OffsetMatrix;
    }

    public struct VertexBoneData
    {
        public const int MAX_BONES_PER_VERTEX = 4;

        public int[] BoneIDs;
        public float[] Weights;

        public static VertexBoneData Create()
        {
            return new VertexBoneData
            {
                BoneIDs = new int[MAX_BONES_PER_VERTEX] { -1, -1, -1, -1 },
                Weights = new float[MAX_BONES_PER_VERTEX]
            };
        }

        public void AddBone(int boneId, float weight)
        {
            for (int i = 0; i < MAX_BONES_PER_VERTEX; i++)
            {
                if (BoneIDs[i] == -1)
                {
                    BoneIDs[i] = boneId;
                    Weights[i] = weight;
                    return;
                }
            }
            // Si ya hay 4 huesos, reemplazar el de menor peso
            int minIndex = 0;
            float minWeight = Weights[0];
            for (int i = 1; i < MAX_BONES_PER_VERTEX; i++)
            {
                if (Weights[i] < minWeight)
                {
                    minWeight = Weights[i];
                    minIndex = i;
                }
            }
            if (weight > minWeight)
            {
                BoneIDs[minIndex] = boneId;
                Weights[minIndex] = weight;
            }
        }

        public void Normalize()
        {
            float total = 0f;
            for (int i = 0; i < MAX_BONES_PER_VERTEX; i++)
                total += Weights[i];

            if (total > 0f)
            {
                for (int i = 0; i < MAX_BONES_PER_VERTEX; i++)
                    Weights[i] /= total;
            }
        }
    }

    public class NodeData
    {
        public string Name { get; set; }
        public Matrix4 Transform { get; set; }
        public List<NodeData> Children { get; set; } = new List<NodeData>();
    }
}