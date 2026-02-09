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
    vec3 fragPos = texture(positionTex, texCoord).xyz;
    vec3 normal = texture(normalTex, texCoord).xyz;
    
    if(length(fragPos) < 0.001)
        return 1.0;
    
    if(length(normal) < 0.01)
        return 1.0;
    
    normal = normalize(normal);
    
    vec2 noiseScale = resolution / 4.0;
    vec3 randomVec = texture(noiseTex, texCoord * noiseScale).xyz;
    
    if(length(randomVec) < 0.01)
        randomVec = vec3(0.0, 0.0, 1.0);
    else
        randomVec = normalize(randomVec);
    
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);
    
    float occlusion = 0.0;
    
    for(int i = 0; i < kernelSize; i++)
    {
        vec3 samplePos = TBN * samples[i];
        samplePos = fragPos + samplePos * radius;
        
        vec4 offset = vec4(samplePos, 1.0);
        offset = projection * offset;
        offset.xyz /= offset.w;
        offset.xyz = offset.xyz * 0.5 + 0.5;
        
        if(offset.x < 0.0 || offset.x > 1.0 || offset.y < 0.0 || offset.y > 1.0)
            continue;
        
        float sampleDepth = texture(positionTex, offset.xy).z;
        
        float rangeCheck = smoothstep(0.0, 1.0, radius / abs(fragPos.z - sampleDepth));
        occlusion += (sampleDepth >= samplePos.z + bias ? 1.0 : 0.0) * rangeCheck;
    }
    
    occlusion = 1.0 - (occlusion / float(kernelSize));
    return occlusion;
}

#endif