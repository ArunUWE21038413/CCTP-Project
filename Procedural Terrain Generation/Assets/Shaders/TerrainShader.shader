Shader "Custom/TerrainPaintShader" {
    Properties {
        // Original Properties
        _MainTex ("Base Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _TextureBlend ("Texture Blend Factor", Range(0, 1)) = 0.5
        
        // Paint Properties
        _PaintMap ("Paint Map", 2D) = "black" {}
        _PaintOpacity ("Paint Opacity", Range(0, 1)) = 1.0
        _PaintIntensity ("Paint Intensity", Range(0, 2)) = 1.0
        
        // Smoothness Properties
        _Smoothness ("Base Smoothness", Range(0, 1)) = 0.5
        _PaintSmoothness ("Paint Smoothness", Range(0, 1)) = 0.2
        _SmoothnessBlend ("Smoothness Blend Factor", Range(0, 1)) = 1.0
    }
    
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        
        CGPROGRAM
        // Define the surface shader model
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        
        // Texture samplers
        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _PaintMap;
        
        // Blend factors
        float _TextureBlend;
        float _PaintOpacity;
        float _PaintIntensity;
        
        // Smoothness parameters
        float _Smoothness;
        float _PaintSmoothness;
        float _SmoothnessBlend;
        
        // Input structure for surface shader
        struct Input {
            float2 uv_MainTex; // UV coordinates for base texture
            float2 uv_NormalMap; // UV coordinates for normal map
            float2 uv_PaintMap; // UV coordinates for paint map
            float4 color : COLOR; // Vertex color
        };
        
        // Surface function to define the material's properties
        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Sample base texture
            float4 baseTexture = tex2D(_MainTex, IN.uv_MainTex);
            
            // Sample paint texture
            float4 paintColor = tex2D(_PaintMap, IN.uv_PaintMap);
            
            // Compute normal from normal map
            float3 normalFromMap = UnpackNormal(tex2D(_NormalMap, IN.uv_NormalMap));
            o.Normal = normalFromMap;
            
            // Blend vertex color with texture color (from original shader)
            float3 blendedAlbedo = lerp(IN.color.rgb, baseTexture.rgb, _TextureBlend);
            
            // Apply paint on top with intensity and opacity control
            float paintInfluence = paintColor.a * _PaintOpacity;
            float3 paintedAlbedo = lerp(blendedAlbedo, paintColor.rgb * _PaintIntensity, paintInfluence);
            
            // Set final albedo
            o.Albedo = paintedAlbedo;
            
            // Set metallic property (no metal)
            o.Metallic = 0.0;
            
            // Calculate smoothness - blend between base and paint smoothness based on paint influence
            // The smoothness blend factor controls how much the paint influences the final smoothness
            float baseSmoothness = _Smoothness;
            float finalSmoothness = lerp(baseSmoothness, _PaintSmoothness, paintInfluence * _SmoothnessBlend);
            o.Smoothness = finalSmoothness;
            
            // Set alpha from texture
            o.Alpha = baseTexture.a;
        }
        ENDCG
    }
    
    // Fallback for older hardware
    FallBack "Diffuse"
}
