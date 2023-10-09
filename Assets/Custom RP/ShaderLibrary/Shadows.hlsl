#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	//float _ShadowDistance;
	float4 _ShadowDistanceFade;
CBUFFER_END

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

struct DirectionalShadowData {
	float strength; // 强度
	int tileIndex;	// 索引id
};

struct ShadowData {
	int cascadeIndex;
	float strength; // 可以处理超出级联范围的情况
};

float FadedShadowStrength (float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;

	data.strength = FadedShadowStrength(
		surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);

	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			if (i == _CascadeCount - 1) {// 当位于最后一个级联时执行级联渐变
				data.strength *= FadedShadowStrength(
					distanceSqr, 1.0 / sphere.w, _ShadowDistanceFade.z
				);
			}
			break;
		}
	}

	if (i == _CascadeCount) {
		data.strength = 0.0;
	}

	data.cascadeIndex = i;
	return data;
}



// 通过SAMPLE_TEXTURE2D_SHADOW宏对阴影图集进行采样
float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

// 获得阴影采样的值
float GetDirectionalShadowAttenuation (DirectionalShadowData data, Surface surfaceWS) {
	
	if (data.strength <= 0.0) {
		return 1.0;
	}
	
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[data.tileIndex],
		float4(surfaceWS.position, 1.0)
	).xyz;

	float shadow = SampleDirectionalShadowAtlas(positionSTS);

	return lerp(1.0, shadow, data.strength);
}

#endif