#version 330 core

#include "includes/ssao.glsl"

out vec4 FragColor;

in vec2 TexCoord;

uniform sampler2D u_ScreenTexture;
uniform sampler2D u_EmissionTexture;
uniform sampler2D u_PositionTexture;
uniform sampler2D u_NormalTexture;
uniform sampler2D u_NoiseTexture;

uniform vec3 u_SSAOKernel[64];
uniform int u_SSAOKernelSize;
uniform float u_SSAORadius;
uniform float u_SSAOBias;
uniform float u_SSAOPower;
uniform int u_SSAOEnabled;

uniform mat4 u_Projection;
uniform mat4 u_View;
uniform vec2 u_Resolution;
uniform float u_Time;

uniform int u_PostProcessEnabled;
uniform int u_ColorCorrectionEnabled;
uniform float u_Brightness;
uniform float u_Contrast;
uniform float u_Saturation;
uniform vec3 u_ColorFilter;

uniform int u_BloomEnabled;
uniform float u_BloomThreshold;
uniform float u_BloomSoftThreshold;
uniform float u_BloomIntensity;
uniform float u_BloomRadius;

uniform int u_GrainEnabled;
uniform float u_GrainIntensity;
uniform float u_GrainSize;

float rand(vec2 co) {
    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

vec3 ApplyColorCorrection(vec3 color)
{
    color += u_Brightness;
    color = ((color - 0.5) * u_Contrast) + 0.5;
    float gray = dot(color, vec3(0.299, 0.587, 0.114));
    color = mix(vec3(gray), color, u_Saturation);
    color *= u_ColorFilter;
    return color;
}

const float PI = 3.14159265359;

vec3 ApplyBloom(vec2 uv)
{
    vec2 texelSize = 1.0 / u_Resolution;
    vec3 bloom = vec3(0.0);
    float totalWeight = 0.0;
    
    int rings = 64;
    int samplesPerRing = 32;
    
    for(int ring = 1; ring <= rings; ring++)
    {
        float t = float(ring) / float(rings);
        float ringRadius = u_BloomRadius * t * 20.0;
        
        for(int i = 0; i < samplesPerRing; i++)
        {
            float angle = (float(i) / float(samplesPerRing)) * 2.0 * PI;
            vec2 offset = vec2(cos(angle), sin(angle)) * ringRadius * texelSize;
            
            float dist = ringRadius;
            float weight = 1.0 / (1.0 + dist * 0.1);
            
            vec3 sample = texture(u_EmissionTexture, uv + offset).rgb;
            bloom += sample * weight;
            totalWeight += weight;
        }
    }
    
    vec3 center = texture(u_EmissionTexture, uv).rgb;
    bloom += center * 5.0;
    totalWeight += 5.0;
    
    bloom /= totalWeight;
    
    return bloom * u_BloomIntensity;
}

vec3 ApplyGrain(vec3 color, vec2 uv)
{
    vec2 grainUV = uv * u_Resolution / u_GrainSize;
    float noise = rand(grainUV + u_Time) * 2.0 - 1.0;
    return color + noise * u_GrainIntensity;
}

void main()
{
    vec3 color = texture(u_ScreenTexture, TexCoord).rgb;
    
    if(u_PostProcessEnabled == 1)
    {
        if(u_SSAOEnabled == 1)
        {
            vec3 viewPos = texture(u_PositionTexture, TexCoord).xyz;
            
            if(length(viewPos) > 0.001)
            {
                float ao = CalculateSSAO(
                    TexCoord,
                    u_PositionTexture,
                    u_NormalTexture,
                    u_NoiseTexture,
                    u_SSAOKernel,
                    u_SSAOKernelSize,
                    u_SSAORadius,
                    u_SSAOBias,
                    u_Projection,
                    u_Resolution
                );
                ao = pow(ao, u_SSAOPower);
                color *= ao;
            }
        }
        
        if(u_BloomEnabled == 1)
        {
            vec3 bloom = ApplyBloom(TexCoord);
            color += bloom;
        }
        
        if(u_ColorCorrectionEnabled == 1)
        {
            color = ApplyColorCorrection(color);
        }
        
        if(u_GrainEnabled == 1)
        {
            color = ApplyGrain(color, TexCoord);
        }
    }
    
    FragColor = vec4(color, 1.0);
}