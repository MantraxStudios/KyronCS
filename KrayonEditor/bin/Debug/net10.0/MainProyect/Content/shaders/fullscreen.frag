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

vec3 ExtractBrightParts(vec3 color)
{
    float brightness = dot(color, vec3(0.2126, 0.7152, 0.0722));
    float knee = u_BloomThreshold * u_BloomSoftThreshold;
    float soft = brightness - u_BloomThreshold + knee;
    soft = clamp(soft, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 0.00001);
    float contribution = max(soft, brightness - u_BloomThreshold);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

vec3 ApplyBloom(vec2 uv)
{
    vec3 bloom = vec3(0.0);
    vec2 texelSize = 1.0 / u_Resolution;
    float radius = u_BloomRadius;
    
    for(float x = -radius; x <= radius; x += 1.0)
    {
        for(float y = -radius; y <= radius; y += 1.0)
        {
            vec2 offset = vec2(x, y) * texelSize;
            vec3 sample = texture(u_EmissionTexture, uv + offset).rgb;
            vec3 bright = ExtractBrightParts(sample);
            float weight = 1.0 / (1.0 + length(vec2(x, y)));
            bloom += bright * weight;
        }
    }
    
    bloom /= ((radius * 2.0 + 1.0) * (radius * 2.0 + 1.0));
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