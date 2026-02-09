#version 330 core

in vec2 v_TexCoord;
out vec4 FragColor;

uniform sampler2D u_ScreenTexture;
uniform sampler2D u_EmissionTexture;
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

float random(vec2 st)
{
    return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

float getLuminance(vec3 color)
{
    return dot(color, vec3(0.299, 0.587, 0.114));
}

vec3 getBloom(vec2 uv)
{
    vec3 bloom = vec3(0.0);
    vec2 texelSize = 1.0 / u_Resolution;
    float totalWeight = 0.0;
    
    int samples = 25;
    for(int x = -samples; x <= samples; x++)
    {
        for(int y = -samples; y <= samples; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize * u_BloomRadius;
            vec3 sample = texture(u_EmissionTexture, uv + offset).rgb;
            
            float dist = length(vec2(x, y)) / float(samples);
            float weight = exp(-dist * dist * 3.0);
            
            bloom += sample * weight;
            totalWeight += weight;
        }
    }
    
    return bloom / totalWeight;
}

vec3 applyColorCorrection(vec3 color)
{
    color += u_Brightness;
    color = (color - 0.5) * u_Contrast + 0.5;
    
    float luma = getLuminance(color);
    color = mix(vec3(luma), color, u_Saturation);
    
    color *= u_ColorFilter;
    
    return color;
}

vec3 applyGrain(vec3 color, vec2 uv)
{
    vec2 grainUV = uv * u_Resolution / u_GrainSize;
    float noise = random(grainUV + u_Time);
    noise = (noise - 0.5) * u_GrainIntensity;
    
    return color + noise;
}

void main()
{
    vec3 color = texture(u_ScreenTexture, v_TexCoord).rgb;
    
    if(u_PostProcessEnabled == 1)
    {
        if(u_BloomEnabled == 1)
        {
            vec3 bloom = getBloom(v_TexCoord);
            color = color + bloom * u_BloomIntensity;
        }
        
        if(u_ColorCorrectionEnabled == 1)
        {
            color = applyColorCorrection(color);
        }
        
        if(u_GrainEnabled == 1)
        {
            color = applyGrain(color, v_TexCoord);
        }
    }
    
    FragColor = vec4(color, 1.0);
}