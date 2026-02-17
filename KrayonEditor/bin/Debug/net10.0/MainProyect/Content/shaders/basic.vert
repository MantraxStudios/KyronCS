#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec3 aTangent;
layout(location = 4) in vec3 aBiTangent;
layout(location = 5) in vec4 aInstanceMatrix0;
layout(location = 6) in vec4 aInstanceMatrix1;
layout(location = 7) in vec4 aInstanceMatrix2;
layout(location = 8) in vec4 aInstanceMatrix3;
layout(location = 9) in ivec4 aBoneIDs;
layout(location = 10) in vec4 aBoneWeights;

out vec3 FragPos;
out vec3 ViewPos;
out vec3 Normal;
out vec3 ViewNormal;
out vec2 TexCoord;
out mat3 TBN;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform int u_UseInstancing;
uniform int u_UseAnimation;

const int MAX_BONES = 256;

// UBO: evita el límite de 1024 registros de uniforms normales
layout (std140) uniform BoneMatricesBlock
{
    mat4 u_BoneMatrices[MAX_BONES];
};

void main()
{
    vec4 skinnedPos;
    vec3 skinnedNormal;
    vec3 skinnedTangent;
    vec3 skinnedBiTangent;

    if (u_UseAnimation == 1)
    {
        mat4 boneTransform = mat4(0.0);
        float totalWeight = 0.0;

        for (int i = 0; i < 4; i++)
        {
            if (aBoneIDs[i] >= 0 && aBoneIDs[i] < MAX_BONES)
            {
                boneTransform += u_BoneMatrices[aBoneIDs[i]] * aBoneWeights[i];
                totalWeight   += aBoneWeights[i];
            }
        }

        if (totalWeight < 0.01)
            boneTransform = mat4(1.0);
        else
            boneTransform /= totalWeight;

        skinnedPos       = boneTransform * vec4(aPosition, 1.0);
        skinnedNormal    = mat3(boneTransform) * aNormal;
        skinnedTangent   = mat3(boneTransform) * aTangent;
        skinnedBiTangent = mat3(boneTransform) * aBiTangent;
    }
    else
    {
        skinnedPos       = vec4(aPosition, 1.0);
        skinnedNormal    = aNormal;
        skinnedTangent   = aTangent;
        skinnedBiTangent = aBiTangent;
    }

    mat4 worldMatrix;

    if (u_UseInstancing == 1)
    {
        worldMatrix = mat4(aInstanceMatrix0, aInstanceMatrix1, aInstanceMatrix2, aInstanceMatrix3);
    }
    else
    {
        worldMatrix = model;
    }

    vec4 worldPos = worldMatrix * skinnedPos;
    FragPos = worldPos.xyz;

    vec4 viewPos = view * worldPos;
    ViewPos = viewPos.xyz;

    mat3 normalMatrix = transpose(inverse(mat3(worldMatrix)));
    Normal = normalize(normalMatrix * skinnedNormal);

    mat3 viewNormalMatrix = mat3(transpose(inverse(view)));
    ViewNormal = normalize(viewNormalMatrix * Normal);

    vec3 T = normalize(normalMatrix * skinnedTangent);
    vec3 B = normalize(normalMatrix * skinnedBiTangent);
    vec3 N = Normal;
    TBN = mat3(T, B, N);

    TexCoord = aTexCoord;

    gl_Position = projection * viewPos;
}