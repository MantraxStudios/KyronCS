#ifndef SSAO_GLSL
#define SSAO_GLSL

float CalculateSSAO(
    vec2 texCoord,
    sampler2D positionTex,
    sampler2D normalTex,
    sampler2D noiseTex,
    vec3 samples[64],
    int kernelSize,
    float radius,
    float bias,
    mat4 projection,
    vec2 resolution
)
{
    vec3 fragPos   = texture(positionTex, texCoord).xyz;
    vec3 normalRaw = texture(normalTex,   texCoord).xyz;

    if (dot(normalRaw, normalRaw) < 0.1)
        return 1.0;

    if (dot(fragPos, fragPos) < 0.0001)
        return 1.0;

    vec3 normal = normalize(normalRaw);

    vec2 noiseScale  = resolution / 4.0;
    vec3 randomVec   = normalize(texture(noiseTex, texCoord * noiseScale).xyz * 2.0 - 1.0);

    vec3 tangent   = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN       = mat3(tangent, bitangent, normal);

    float occlusion  = 0.0;
    float validCount = 0.0;

    for (int i = 0; i < kernelSize; i++)
    {
        vec3 samplePos = fragPos + (TBN * samples[i]) * radius;

        vec4 offset = projection * vec4(samplePos, 1.0);
        offset.xyz /= offset.w;
        offset.xyz  = offset.xyz * 0.5 + 0.5;

        if (offset.x < 0.0 || offset.x > 1.0 ||
            offset.y < 0.0 || offset.y > 1.0)
            continue;

        float sampleDepth = texture(positionTex, offset.xy).z;

        if (abs(sampleDepth) < 0.0001)
            continue;

        float depthDiff  = abs(fragPos.z - sampleDepth);
        float rangeCheck = 1.0 - smoothstep(0.0, radius, depthDiff);

        occlusion  += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;
        validCount += rangeCheck;
    }

    if (validCount < 0.001)
        return 1.0;

    return clamp(1.0 - (occlusion / max(validCount, 1.0)), 0.0, 1.0);
}

#endif