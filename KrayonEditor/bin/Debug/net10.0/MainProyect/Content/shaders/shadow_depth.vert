#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 5) in vec4 aInstanceMatrix0;
layout(location = 6) in vec4 aInstanceMatrix1;
layout(location = 7) in vec4 aInstanceMatrix2;
layout(location = 8) in vec4 aInstanceMatrix3;
layout(location = 9) in ivec4 aBoneIDs;
layout(location = 10) in vec4 aBoneWeights;

uniform mat4 u_LightSpaceMatrix;
uniform mat4 model;
uniform int u_UseInstancing;
uniform int u_UseAnimation;

const int MAX_BONES = 256;
layout(std140) uniform BoneMatricesBlock
{
    mat4 u_BoneMatrices[MAX_BONES];
};

void main()
{
    vec4 skinnedPos = vec4(aPosition, 1.0);

    if (u_UseAnimation == 1)
    {
        mat4 boneTransform = mat4(0.0);
        float totalWeight = 0.0;
        for (int i = 0; i < 4; i++)
        {
            if (aBoneIDs[i] >= 0 && aBoneIDs[i] < MAX_BONES)
            {
                boneTransform += u_BoneMatrices[aBoneIDs[i]] * aBoneWeights[i];
                totalWeight += aBoneWeights[i];
            }
        }
        if (totalWeight < 0.01) boneTransform = mat4(1.0);
        else boneTransform /= totalWeight;
        skinnedPos = boneTransform * vec4(aPosition, 1.0);
    }

    mat4 worldMatrix;

    if (u_UseInstancing == 1)
    {
        worldMatrix = mat4(
            vec4(aInstanceMatrix0.x, aInstanceMatrix1.x, aInstanceMatrix2.x, aInstanceMatrix3.x),
            vec4(aInstanceMatrix0.y, aInstanceMatrix1.y, aInstanceMatrix2.y, aInstanceMatrix3.y),
            vec4(aInstanceMatrix0.z, aInstanceMatrix1.z, aInstanceMatrix2.z, aInstanceMatrix3.z),
            vec4(aInstanceMatrix0.w, aInstanceMatrix1.w, aInstanceMatrix2.w, aInstanceMatrix3.w)
        );
    }
    else
    {
        worldMatrix = model;
    }

    gl_Position = u_LightSpaceMatrix * worldMatrix * skinnedPos;
}
