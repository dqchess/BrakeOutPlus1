// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// This function isn't supported, functions under this can't be looped through
#undef UNITY_LIGHT_PROBE_PROXY_VOLUME
// Forces unity to implement posWorld inside VertexOutputForwardBase
#define UNITY_SPECCUBE_BOX_PROJECTION 1
#include "UnityStandardCore.cginc"

// TRANSFORMS
fixed _Tiling, _Density, _DensityLow, _DensityHigh, _SphereHorizonStretch;
sampler2D _PerlinNormalMap, _DensityMap;
fixed4 _PerlinNormalMap_ST, _DensityMap_ST;
fixed4 _WindDirection, _CloudTransform, _CloudSpherePosition, _CloudSphereDimensions;

// TIME
fixed4 _TimeEditor;
fixed _TimeMult, _TimeMultSecondLayer;

// COLOR AND SHADING
fixed4 _VisiblePointLights[64], _VisiblePointLightsColor[64];
int _VisibleLightCount;
fixed4 _BaseColor,  _LightAttenuation, _Shading, _ShadowColor;
fixed _DepthColor, _NormalsIntensity, _Normalized, _IndirectContribution;
fixed _Alpha, _AlphaCut, _DistanceBlend;

// RAYMARCHER
sampler2D _CameraDepthTexture;
fixed4x4 _ToWorldMatrix;
int _MaxSteps;
fixed _StepSize, _StepNearSurface, _StepSkip, _OpacityGain;
int _SkipPixel;
fixed _LodBase, _LodOffset, _DrawDistance, _ShadowDrawDistance;

float _OrthographicPerspective;

VertexOutputForwardBase vertForwardModified (VertexInput v)
{
	VertexOutputForwardBase o = vertForwardBase(v);
	o.tex = UnityObjectToClipPos(v.vertex );
	return o;
}


inline half4 VertexGIForwardReplacement(fixed3 posWorld, half3 normalWorld)
{
	half4 ambientOrLightmapUV = 0;
	#if UNITY_VERSION >= 530
		#if UNITY_SHOULD_SAMPLE_SH
			#ifdef VERTEXLIGHT_ON
				ambientOrLightmapUV.rgb = Shade4PointLights (
					unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
					unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
					unity_4LightAtten0, posWorld, normalWorld);
			#endif

			ambientOrLightmapUV.rgb = ShadeSHPerVertex (normalWorld, ambientOrLightmapUV.rgb);		
		#endif
	#else
		#if UNITY_SHOULD_SAMPLE_SH
			#if UNITY_SAMPLE_FULL_SH_PER_PIXEL
				ambientOrLightmapUV.rgb = 0;
			#elif (SHADER_TARGET < 30)
				ambientOrLightmapUV.rgb = ShadeSH9(half4(normalWorld, 1.0));
			#else
				// Optimization: L2 per-vertex, L0..L1 per-pixel
				ambientOrLightmapUV.rgb = ShadeSH3Order(half4(normalWorld, 1.0));
			#endif
			// Add approximated illumination from non-important point lights
			#ifdef VERTEXLIGHT_ON
				ambientOrLightmapUV.rgb += Shade4PointLights (
					unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
					unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
					unity_4LightAtten0, posWorld, normalWorld);
			#endif
		#endif
	#endif

	return ambientOrLightmapUV;
}

half4 BRDF1_Unity_PBSMODIFIED (half3 diffColor, half3 specColor, half oneMinusReflectivity, half oneMinusRoughness,
	half3 normal, half3 viewDir,
	UnityLight light, UnityIndirect gi)
{
	gi.diffuse *= _IndirectContribution;
	//gi.specular *= _IndirectContribution;
	
	half roughness = 1-oneMinusRoughness;
	half3 halfDir = normalize (light.dir + viewDir);
	
	fixed nl = dot(normal, light.dir);
	nl = saturate(lerp(nl, nl*0.5+0.5, _Normalized));

	//half nh = BlinnTerm (normal, halfDir);
	half nv = DotClamped (normal, viewDir);
	//half lv = DotClamped (light.dir, viewDir);
	half lh = DotClamped (light.dir, halfDir);
	/*
#if UNITY_BRDF_GGX
	half V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
	half D = GGXTerm (nh, roughness);
#else
	half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
	half D = NDFBlinnPhongNormalizedTerm (nh, RoughnessToSpecPower (roughness));
#endif*/

	half nlPow5 = Pow5 (1-nl);
	half nvPow5 = Pow5 (1-nv);
	half Fd90 = 0.5 + 2 * lh * lh * roughness;
	half disneyDiffuse = (1 + (Fd90-1) * nlPow5) * (1 + (Fd90-1) * nvPow5);
	/*
	// HACK: theoretically we should divide by Pi diffuseTerm and not multiply specularTerm!
	// BUT 1) that will make shader look significantly darker than Legacy ones
	// and 2) on engine side "Non-important" lights have to be divided by Pi to in cases when they are injected into ambient SH
	// NOTE: multiplication by Pi is part of single constant together with 1/4 now
	half specularTerm = (V * D) * (UNITY_PI/4); // Torrance-Sparrow model, Fresnel is applied later (for optimization reasons)
	if (IsGammaSpace())
		specularTerm = sqrt(max(1e-4h, specularTerm));
	specularTerm = max(0, specularTerm * nl);*/

	half diffuseTerm = disneyDiffuse * nl;

	//half grazingTerm = saturate(oneMinusRoughness + (1-oneMinusReflectivity));
	/*
    half3 color =	diffColor * (gi.diffuse + light.color * diffuseTerm)
                    + specularTerm * light.color * FresnelTerm (specColor, lh)
					+ gi.specular * FresnelLerp (specColor, grazingTerm, nv);*/
	half3 color =	diffColor * lerp( _Shading.rgb, _Shading.a, (gi.diffuse + light.color * diffuseTerm));
                    //+ specularTerm * light.color * FresnelTerm (specColor, lh)
					//+ gi.specular * FresnelLerp (specColor, grazingTerm, nv);

	return half4(color, 1);
}

inline fixed3 NormalLighting(FragmentCommonData s, fixed3 samplePos, fixed3 normalDir)
{
	s.normalWorld = normalDir;
	s.posWorld = samplePos;
#if UNITY_VERSION >= 550
	UnityLight mainLight = MainLight ();
#else
	UnityLight mainLight = MainLight (normalDir);
#endif
	half atten = 1.0f;

	half occlusion = 1.0f;
#if UNITY_VERSION >= 520
	UnityGI gi = FragmentGI (s, occlusion, VertexGIForwardReplacement(samplePos, normalDir), atten, mainLight);
#else
	UnityGI gi = FragmentGI (s.posWorld, occlusion,  VertexGIForwardReplacement(samplePos, normalDir), atten, s.oneMinusRoughness, s.normalWorld, s.eyeVec, mainLight);
#endif
#if UNITY_VERSION >= 550
	half3 c = BRDF1_Unity_PBSMODIFIED (s.diffColor, s.specColor, s.oneMinusReflectivity, 1.0, normalDir, -s.eyeVec, gi.light, gi.indirect);
#else
	half3 c = BRDF1_Unity_PBSMODIFIED (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, normalDir, -s.eyeVec, gi.light, gi.indirect);
#endif
	//c += UNITY_BRDF_GI (s.diffColor, s.specColor, s.oneMinusReflectivity, s.oneMinusRoughness, normalDir, -s.eyeVec, occlusion, gi);
	return lerp(fixed3(1, 1, 1), c, _BaseColor.a);
}

inline fixed4 SampleClouds(fixed2 UV, fixed sampleHeight, fixed lod) {
	//UV = //tex2Dlod(_PerlinNormalMap, fixed4(UV, 0, lod)).a;
	fixed2 baseAnimation = (_Time.g + _TimeEditor.g) * 0.001 * _WindDirection.rb;
	fixed2 worldUV = (UV+_CloudTransform.zw)/_Tiling;

	fixed2 newUV = worldUV + (baseAnimation * _TimeMult);
	fixed2 newUV2 = worldUV + (baseAnimation * _TimeMultSecondLayer) + fixed2(0.0, 0.5);
	
	fixed4 cloudTexture = tex2Dlod(_PerlinNormalMap, fixed4(newUV, 0, lod));
	fixed4 cloudTexture2 = tex2Dlod(_PerlinNormalMap, fixed4(newUV2, 0, lod));
	
	cloudTexture.xyz = cloudTexture.xyz*2-1;
	cloudTexture2.xyz = cloudTexture2.xyz*2-1;

	fixed density = _Density;
	
#if _DENSITYMAP
	fixed densityMap = tex2Dlod(_DensityMap, fixed4((UV+_DensityMap_ST.zw)/_DensityMap_ST.xy/*TRANSFORM_TEX(UV, _DensityMap)*/, 0, lod)).r;
	density += lerp(_DensityLow, _DensityHigh, densityMap);
#endif

	fixed4 baseCloud = ((cloudTexture + density)*sampleHeight) - cloudTexture2;
	baseCloud.a = saturate(baseCloud.a*_Alpha);
	baseCloud.rgb = normalize(baseCloud.rgb);

	return baseCloud;
}

inline fixed SampleCloudsShadow(fixed2 UV, fixed sampleHeight, fixed lod) {
	//UV = //tex2Dlod(_PerlinNormalMap, fixed4(UV, 0, lod)).a;
	fixed2 baseAnimation = (_Time.g + _TimeEditor.g) * 0.001 * _WindDirection.rb;
	fixed2 worldUV = (UV+_CloudTransform.zw)/_Tiling;

	fixed2 newUV = worldUV + (baseAnimation * _TimeMult);
	fixed2 newUV2 = worldUV + (baseAnimation * _TimeMultSecondLayer) + fixed2(0.0, 0.5);
	
	fixed cloudTexture = tex2Dlod(_PerlinNormalMap, fixed4(newUV, 0, lod)).a;
	fixed cloudTexture2 = tex2Dlod(_PerlinNormalMap, fixed4(newUV2, 0, lod)).a;

	fixed density = _Density;
	
#if _DENSITYMAP
	fixed densityMap = tex2Dlod(_DensityMap, fixed4((UV+_DensityMap_ST.zw)/_DensityMap_ST.xy/*TRANSFORM_TEX(UV, _DensityMap)*/, 0, lod)).r;
	density += lerp(_DensityLow, _DensityHigh, densityMap);
#endif

	fixed baseCloud = ((cloudTexture + density)*sampleHeight) - cloudTexture2;
	baseCloud = saturate(baseCloud*_Alpha);

	return baseCloud;
}

inline fixed3 IntersectionOnPlane(fixed3 offsetOrthogonalToPlane, fixed3 rayDirection, out bool oob)
{
	fixed dotToSurface = dot(normalize(offsetOrthogonalToPlane), rayDirection);
	if(dotToSurface <= 0.0f)
	{
		oob = true;
		return fixed3(0, 0, 0);
	}
	oob = false;
	return rayDirection * length(offsetOrthogonalToPlane) / dotToSurface;
}

inline fixed3 RaySphereIntersection(fixed3 spherePos, fixed3 sphereSize, float sphereRadius, fixed3 rayPos, fixed3 rayDirection, fixed fromOutside, out bool oob)
{
    fixed3 rayPosToSpherePos = (spherePos - rayPos);
    float sphereDirPerpOffset = dot(rayPosToSpherePos, rayDirection);
    fixed3 dirPerpToSpherePos = rayDirection * sphereDirPerpOffset;

    fixed3 centerToPerp = (dirPerpToSpherePos - rayPosToSpherePos);///sphereSize;
	float centerToPerpMag = length(centerToPerp);
    // Are we still inside the sphere ?
    if (centerToPerpMag < sphereRadius)
    {
        float collisionOffset = sqrt(sphereRadius * sphereRadius - centerToPerpMag * centerToPerpMag);
        fixed3 collisionPoint = spherePos + (centerToPerp + rayDirection * collisionOffset * fromOutside);
		
		if (dot(collisionPoint - rayPos, rayDirection) > 0)
        {
            oob = false;
            return collisionPoint;
        }
    }
    oob = true;
    return 0;
}

inline fixed3 RaySphereIntersectionAlwaysFromInside(fixed3 spherePos, fixed3 sphereSize, float sphereRadius, fixed3 rayPos, fixed3 rayDirection)
{

	fixed3 rayPosToSpherePos = (spherePos - rayPos);
    float sphereDirPerpOffset = dot(rayPosToSpherePos, rayDirection);
    fixed3 dirPerpToSpherePos = rayDirection * sphereDirPerpOffset;

    fixed3 centerToPerp = (dirPerpToSpherePos - rayPosToSpherePos);///sphereSize;
	float centerToPerpMag = length(centerToPerp);
    // Are we still inside the sphere ?
    /*if (centerToPerpMag < sphereRadius)
    {*/
        float collisionOffset = sqrt(sphereRadius * sphereRadius - centerToPerpMag * centerToPerpMag);
        fixed3 collisionPoint = spherePos + (centerToPerp + rayDirection * collisionOffset);
		return collisionPoint;
		/*if (dot(collisionPoint - rayPos, rayDirection) > 0)
        {
            return collisionPoint;
        }*/
    //}
    return 0;

	/*
	fixed3 rayPosToSpherePos = (spherePos - rayPos);
    float sphereDirPerpOffset = dot(rayPosToSpherePos, rayDirection);
    fixed3 dirPerpToSpherePos = rayDirection * sphereDirPerpOffset;

    fixed3 centerToPerp = (dirPerpToSpherePos - rayPosToSpherePos);///sphereSize;
	float centerToPerpMag = length(centerToPerp);

    float collisionOffset = sqrt(sphereRadius * sphereRadius - centerToPerpMag * centerToPerpMag);
    return spherePos + (centerToPerp + rayDirection * collisionOffset);*/
}

inline fixed3 IntersectionInSphere(fixed3 spherePos, fixed3 sphereSize, float sphereRadius, float sphereDepth, fixed3 rayPos, fixed3 rayDirection, out bool oob)
{
    oob = false;

    // Is inside spheres, try to project along dir to closest border inside
    if (length((spherePos - rayPos)/*sphereSize*/) < sphereRadius - sphereDepth)
    {
        return RaySphereIntersection(spherePos, sphereSize, sphereRadius - sphereDepth, rayPos, rayDirection, 1, oob);
    }
    // Is outside spheres, try to project along dir to closest border outside
    else if (length((spherePos - rayPos)/*sphereSize*/) > sphereRadius + sphereDepth)
    {
        return RaySphereIntersection(spherePos, sphereSize, sphereRadius + sphereDepth, rayPos, rayDirection, -1, oob);
    }
	else
	{
		// Is between big sphere's surface and small sphere's surface, do nothing
		oob = false;
		return rayPos;
	}
}








// Offset is the percentage of tile displacement, 1 = a tile, 0.5 = half a tile
inline fixed CreateGrid(fixed2 pixelPos, fixed2 offset, fixed resolution)
{
	fixed2 transform = round(frac((offset*0.5f*resolution + pixelPos) / resolution));
	return distance(transform.r+transform.g, 1.0f);
}

inline fixed3 ComputePointLightSimple(fixed3 samplePos, fixed4 transform, fixed3 color)
{
	return (1-min(distance(transform.xyz, samplePos)/transform.w, 1))*color.rgb;
}

inline fixed3 ComputePointLightComplex(fixed3 samplePos, fixed3 sampleNormal, fixed opacity, fixed4 transform, fixed3 color)
{
	float3 delta = transform.xyz - samplePos;
	return (1-min(length(delta)/transform.w, 1))*((dot(normalize(delta), sampleNormal)+1)*0.5) * color.rgb * opacity;
}


inline fixed CloudCenterBounds()
{
	return _CloudTransform.x;
}

inline fixed CloudUpperBounds()
{
	return CloudCenterBounds() + _CloudTransform.y;
}

inline fixed CloudLowerBounds()
{
	return CloudCenterBounds() - _CloudTransform.y;
}



inline fixed4 WorldPosFromDepth(fixed2 uv)
{
	fixed vz = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
	vz = min(_DrawDistance, vz);
	// Depth stops at far clipping plane even if there aren't any object there. Extend depth to drawdistance
	if(distance(vz, _ProjectionParams.z) < _ProjectionParams.z/100.0f)
		vz = _DrawDistance;
    fixed2 p11_22 = fixed2(unity_CameraProjection._11, unity_CameraProjection._22);
    fixed3 vpos = fixed3((uv * 2 - 1) / p11_22, -1) * vz;
	fixed4 wpos = mul(_ToWorldMatrix, fixed4(vpos, 1));
    return wpos;
}

inline fixed DistFromCloudCenter(fixed3 samplePos)
{
#if _SPHEREMAPPED
	return distance(samplePos, _CloudSpherePosition + normalize(samplePos - _CloudSpherePosition)*CloudCenterBounds()/**_CloudSphereDimensions*/) / _CloudTransform.y;
#endif
#if !_SPHEREMAPPED
	return distance(CloudCenterBounds(), samplePos.y) / _CloudTransform.y;
#endif
}

inline fixed DistFromCloudCenterSigned(fixed3 samplePos)
{
#if _SPHEREMAPPED
	fixed3 positiveDir = (samplePos - _CloudSpherePosition)/* * _CloudSphereDimensions*/;
	fixed3 currentDir = samplePos - (_CloudSpherePosition+normalize(positiveDir)*CloudCenterBounds()/**_CloudSphereDimensions*/);
	return sign(dot(positiveDir, currentDir)) * DistFromCloudCenter(samplePos);
#endif
#if !_SPHEREMAPPED
	return (samplePos.y - CloudCenterBounds()) / _CloudTransform.y;
#endif
}

inline bool IsOutsideCloudBounds(fixed3 samplePos, fixed sampleDir)
{
#if _SPHEREMAPPED
	// Only check for outside sphere, when inside we'll use 
	fixed dist = length((samplePos - _CloudSpherePosition)/*_CloudSphereDimensions*/);
	return dist > CloudUpperBounds() + 0.1f;
#endif
#if !_SPHEREMAPPED
	return samplePos.y > CloudUpperBounds() + 0.1f || samplePos.y < CloudLowerBounds() - 0.1f;
#endif
}
#if _SPHEREMAPPED
// put sample pos on the closest point forward in sphere bounds, doesnt work properly for some reason
inline fixed3 IsInsideEmptySphere(fixed3 samplePos, fixed sampleDir)
{
	if(length((samplePos - _CloudSpherePosition)/* * _CloudSphereDimensions*/) < CloudLowerBounds() -0.1f)
		return RaySphereIntersectionAlwaysFromInside(_CloudSpherePosition, _CloudSphereDimensions, CloudLowerBounds(), samplePos, sampleDir);

	return samplePos;
}
#endif















inline fixed3 PlaceSamplePosOnBounds(fixed3 samplePos, fixed3 initDir, out bool oob)
{	
#if _SPHEREMAPPED
	oob = true;
	samplePos = IntersectionInSphere(_CloudSpherePosition, _CloudSphereDimensions, CloudCenterBounds(), _CloudTransform.y, samplePos, initDir, oob);
	return samplePos;
#endif
#if !_SPHEREMAPPED
	oob = false;
	if(samplePos.y > CloudUpperBounds())
	{
		fixed3 offsetOrthoToBounds = fixed3(samplePos.x, CloudUpperBounds(), samplePos.z) - samplePos;
		fixed3 offsetToBounds = IntersectionOnPlane(offsetOrthoToBounds, initDir, oob);

		return samplePos += offsetToBounds;
	}
	if(samplePos.y < CloudLowerBounds())
	{
		fixed3 offsetOrthoToBounds = fixed3(samplePos.x, CloudLowerBounds(), samplePos.z) - samplePos;
		fixed3 offsetToBounds = IntersectionOnPlane(offsetOrthoToBounds, initDir, oob);

		return samplePos += offsetToBounds;
	}
	return samplePos;
#endif
}

// Not really precise but works well enough for now
inline fixed3 TransformNormalsAroundSphere(fixed3 normalPlanar, fixed3 samplePosOnSphere)
{
	fixed dotF = dot(samplePosOnSphere, fixed3(0, 0, 1));
    fixed dotU = dot(samplePosOnSphere, fixed3(0, 1, 0));
    fixed3 fwdDir = normalize(fixed3(0, 0, 1) * dotU + fixed3(0, -1, 0) * dotF);
    fixed3 rightDir = normalize(cross(samplePosOnSphere, fwdDir));
	fixed scalar = clamp(samplePosOnSphere.y*2, -1, 1);
	return normalize(normalPlanar.x * rightDir * scalar + normalPlanar.y * samplePosOnSphere + normalPlanar.z * fwdDir * scalar);
}





















inline fixed4 RaymarchShadowClouds(fixed4 scenePixelPos, fixed scenePixelDepth, fixed renderedAlpha)
{
	if(scenePixelDepth > _ShadowDrawDistance || renderedAlpha >= .99f)
		return 0.0f;
	// Init raymarching data
	// Start after clipping planes
	fixed3 initDir = _WorldSpaceLightPos0;
	fixed3 initPos = scenePixelPos;
	fixed3 samplePos = initPos;
	fixed3 offset = initDir * _StepSize;
	fixed distanceTravelled = 0.0f;

	fixed shadow = 0.0f;
	
	// PLACE SAMPLEPOS ON CLOUD BOUNDS
	bool oob;
	samplePos = PlaceSamplePosOnBounds(samplePos, initDir, oob);
	if(oob)
		return 0;
		
	// RAYMARCH
	#if !SHADER_API_OPENGL && !SHADER_API_GLES3 && !SHADER_API_GLES
	[loop]
	#endif
	for(int i = 0; i < _MaxSteps; i++)
	{
		if(shadow >= 1.0f)
			break;
		/*
	#if _SPHEREMAPPED
		samplePos = IsInsideEmptySphere(samplePos, initDir);
	#endif*/

		// If current pos is outside of cloud bounds quit loop
		if(IsOutsideCloudBounds(samplePos, initDir))
			break;
		
		// If distance travelled is greater than draw distance or scene depth, quit loop
		distanceTravelled = distance(samplePos, initPos);
		if(distanceTravelled > _ShadowDrawDistance)
			break;

		fixed dist = DistFromCloudCenter(samplePos);

	#if _SPHEREMAPPED
		fixed3 deltaFromCenter = samplePos - _CloudSpherePosition;
		fixed textureSample = SampleCloudsShadow(fixed2(deltaFromCenter.x, deltaFromCenter.z) * pow((CloudUpperBounds()-abs(deltaFromCenter.y))/CloudUpperBounds(), _SphereHorizonStretch) , 1.0f-saturate(dist), _LodBase+sqrt(sqrt(distanceTravelled) * _LodOffset));
	#endif
	#if !_SPHEREMAPPED
		fixed textureSample = SampleCloudsShadow(fixed2(samplePos.x, samplePos.z), 1.0f-saturate(dist), _LodBase+sqrt(sqrt(distanceTravelled) * _LodOffset));
	#endif
		fixed3 newSampleOffset = offset * (1.0f+sqrt(distanceTravelled)*_StepSkip);

		// Is inside cloud volume ?
		if(textureSample > dist)
		{
			// Opacity based on position inside the cloud and ray passthrough
			fixed opacityGain = _OpacityGain * distance(textureSample, dist);
			shadow += opacityGain;
			newSampleOffset = offset * (1.0f+sqrt(distanceTravelled)*_StepSkip) * _StepNearSurface * (1.0f-textureSample);
		}
		samplePos+=newSampleOffset;
	}
	return fixed4(_ShadowColor.rgb, saturate(shadow)*_ShadowColor.a);
}















inline fixed4 RaymarchClouds(FragmentCommonData s, fixed depth)
{
	// Init raymarching data
	fixed3 initDir = s.eyeVec;
	fixed3 initPos = s.posWorld;
	fixed3 samplePos = initPos;
	fixed3 offset = initDir * _StepSize;
	fixed distanceTravelled = 0.0f;

	fixed4 colors = 0.0f;

	// PLACE SAMPLEPOS ON CLOUD BOUNDS
	bool oob;
	samplePos = PlaceSamplePosOnBounds(samplePos, initDir, oob);
	if(oob)
		return 0;
		
	// RAYMARCH
	fixed distInsideCloud = 0;
	#if !SHADER_API_OPENGL && !SHADER_API_GLES3 && !SHADER_API_GLES
	[loop]
	#endif
	for(int i = 0; i < _MaxSteps; i++)
	{
		if(colors.a >= 1.0f)
			break;

		/*
	#if _SPHEREMAPPED
		samplePos = IsInsideEmptySphere(samplePos, initDir);
	#endif*/

		// If current pos is outside of cloud bounds quit loop
		if(IsOutsideCloudBounds(samplePos, initDir))
			break;

		// If distance travelled is greater than draw distance or scene depth, quit loop
		distanceTravelled = distance(samplePos, initPos);
		if(distanceTravelled > depth)
			break;

		fixed dist = DistFromCloudCenter(samplePos);
	#if _SPHEREMAPPED
		fixed3 deltaFromCenter = samplePos - _CloudSpherePosition;
		fixed4 textureSample = SampleClouds(fixed2(deltaFromCenter.x, deltaFromCenter.z) * pow((CloudUpperBounds()-abs(deltaFromCenter.y))/CloudUpperBounds(), _SphereHorizonStretch)  , 1.0f-saturate(dist), _LodBase+sqrt(sqrt(distanceTravelled) * _LodOffset));
	#endif
	#if !_SPHEREMAPPED
		fixed4 textureSample = SampleClouds(fixed2(samplePos.x, samplePos.z), 1.0f-saturate(dist), _LodBase+sqrt(sqrt(distanceTravelled) * _LodOffset));
	#endif
		fixed3 newSampleOffset = offset * (1.0f+sqrt(distanceTravelled)*_StepSkip);

		// Is inside cloud volume ?
		if(textureSample.w > dist)
		{
			// Opacity based on position inside the cloud and ray passthrough
			fixed opacityGain = _OpacityGain * distance(textureSample.w, dist);
			distInsideCloud = opacityGain;
			colors.a += opacityGain;
			fixed heightDiff = DistFromCloudCenterSigned(samplePos);
			fixed3 baseSampleColor = lerp(1.0f, heightDiff * 0.5f + 0.5f, _DepthColor) * opacityGain;
			#if _NORMALMAP
				fixed3 normals = normalize(fixed3(textureSample.x*_NormalsIntensity, heightDiff, textureSample.y*_NormalsIntensity));
			#if _SPHEREMAPPED
				normals = TransformNormalsAroundSphere(normals, normalize(deltaFromCenter));
			#endif
				fixed lightAttenuation = saturate((1.0f-saturate(distanceTravelled))*distInsideCloud*_LightAttenuation.a*50.0f);
				normals = normalize(lerp(normals, -_WorldSpaceLightPos0.xyz,lightAttenuation));
				baseSampleColor *= NormalLighting(s, samplePos, normals);
			#endif
			colors.rgb += baseSampleColor;
			
			
			// Point light contribution
		#if _HQPOINTLIGHT
			for(int j = 0; j < _VisibleLightCount; j++)
			{
				colors.rgb += ComputePointLightComplex(samplePos, normals, opacityGain, _VisiblePointLights[j], _VisiblePointLightsColor[j].rgb);
			}
		#endif

			newSampleOffset = offset * (1.0f+sqrt(distanceTravelled)*_StepSkip) * _StepNearSurface * (1.0f-textureSample.w);
		}
		samplePos += newSampleOffset;
	}
	colors.a = saturate(colors.a)*(1-saturate(pow(distanceTravelled/_DrawDistance,1.0f/_DistanceBlend)));
	// Re-equilibrate transparent colors
	colors.rgb += (1.0f-colors.a)*colors.rgb;
	// Light attenuation inside clouds
	colors.rgb = lerp(colors.rgb, _LightAttenuation.rgb, saturate((1.0f-saturate(distanceTravelled))*distInsideCloud*_LightAttenuation.a*50.0f));
	
#if !_HQPOINTLIGHT
	// Point light contribution
	for(int j = 0; j < _VisibleLightCount; j++)
	{
		colors.rgb += ComputePointLightSimple(samplePos, _VisiblePointLights[j], _VisiblePointLightsColor[j].rgb);
	}
#endif

	return colors;
}



half4 fragForwardClouds (VertexOutputForwardBase i) : SV_Target
{
	fixed grabSign = abs(_ProjectionParams.x);

	fixed4 screenPos = fixed4( i.tex.xy / i.tex.w, 0, 0 );
    screenPos.y *= _ProjectionParams.x;
    fixed2 sceneUVs = fixed2(1,grabSign)*screenPos.xy*0.5+0.5;
	fixed4 sceneDepthPos = WorldPosFromDepth(sceneUVs);
	fixed sceneDepth = distance(sceneDepthPos, i.posWorld);

	FRAGMENT_SETUP(s)
	//return fixed4(0, 0, 0, 1);
	s.diffColor = _BaseColor;
#if UNITY_OPTIMIZE_TEXCUBELOD
	s.reflUVW		= i.reflUVW;
#endif

	float3 fakePerspDir = lerp(i.posWorld.xyz - _WorldSpaceCameraPos, UNITY_MATRIX_IT_MV[2].xyz, _OrthographicPerspective * UNITY_MATRIX_P[3][3]);
	i.eyeVec = NormalizePerVertexNormal(fakePerspDir);
	


	


	fixed4 clouds = 0;
	fixed4 shadows = 0;
	#if _SKIPPIXELONCE || _SKIPPIXELTWICE
		fixed2 pixelPosition = fixed2(_ScreenParams.x,_ScreenParams.y)*sceneUVs.xy;
		#if _SKIPPIXELONCE
			fixed grid = CreateGrid(pixelPosition, fixed2(.25f, .25f), 2);
		#else
			fixed grid = CreateGrid(pixelPosition, fixed2(.5f, .5f), 4);
		#endif
		if(grid != 0)
		{
			#if !_RENDERSHADOWSONLY
				clouds = RaymarchClouds(s, sceneDepth);
			#endif
			#if _RENDERSHADOWS
				shadows = RaymarchShadowClouds(sceneDepthPos, sceneDepth, clouds.a);
			#endif
		}
		if(grid == 0)
			clouds = fwidth(clouds) * 0.5f * (1.0-grid) + clouds;

	#else// !_SKIPPIXELONCE && !_SKIPPIXELTWICE
		#if !_RENDERSHADOWSONLY
			clouds = RaymarchClouds(s, sceneDepth);
		#endif
		#if _RENDERSHADOWS
			shadows = RaymarchShadowClouds(sceneDepthPos, sceneDepth, clouds.a);
		#endif
	#endif
	clouds += fixed4(shadows.r*shadows.a, shadows.g*shadows.a, shadows.b*shadows.a, shadows.a)*(1-clouds.a);
	
	if(clouds.a <= _AlphaCut)
		discard;

	return clouds;
}