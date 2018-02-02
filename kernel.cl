#define GLINTEROP

#ifdef GLINTEROP
void Write_image(write_only image2d_t a, uint bitarray, uint xoffset, uint yoffset, uint x, uint y, uint reso)
#else
void Write_image(__global int* a, uint bitarray, uint xoffset, uint yoffset, uint x, uint y)
#endif
{
	if(y < yoffset || y >= yoffset + reso || x < xoffset / 32 || x >= (xoffset + reso + 31) / 32 ) return;
	int p = x * 32 - xoffset; int q = (x + 1) * 32 - xoffset;
	int i = 0;
	int j = 32;
	if(p < 0)
	{
		i += xoffset % 32;
	}
	if(q >= reso)
	{
		j -= q - reso;
	}
	while(i < j)
	{
		#ifdef GLINTEROP
		int2 pos = (int2)(p + i, y - yoffset);
		float z = (float)((bitarray >> i) << 31);
		write_imagef(a, pos, (float4)(z, z, z, z));
		#else
		a[p + i + (y - yoffset) * reso] = (float)((bitarray >> i) << 31) * 0xffffff;
		#endif
		i++;
	}
}

uint GetBit(uint x, uint y, uint pw, __global uint* second)
{
	return (second[y * pw + (x >> 5)] >> (int)(x & 31)) & 1U; 
}

void BitSet(uint x, uint y, uint pw, __global uint* pattern) 
{
	pattern[y * pw + (x >> 5)] |= 1U << (int)(x & 31); 
}

#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, __global write_only uint* pattern, __global uint* second, uint pw, uint ph, uint xoffset, uint yoffset, uint reso)
#else
__kernel void device_function( __global int* a, __global write_only uint* pattern, __global uint* second, uint pw, uint ph, uint xoffset, uint yoffset)
#endif
{
	uint idx = get_global_id( 0 );
	uint idy = get_global_id( 1 );
	pattern[idy * pw + idx] = 0;
	if(idy == 0 || idy == ph - 1) return;

	for(int i = 0; i < 32; i++)
	{
		uint x = (idx * 32) + i;
		uint y = idy;
		if(x == 0 || x == 1727) continue;
		uint n = GetBit(x - 1, y - 1, pw, second) 
		+ GetBit(x, y - 1, pw, second) 
		+ GetBit(x + 1, y - 1, pw, second) 
		+ GetBit(x - 1, y, pw, second) 
		+ GetBit(x + 1, y, pw, second) 
		+ GetBit(x - 1, y + 1, pw, second) 
		+ GetBit(x, y + 1, pw, second) 
		+ GetBit(x + 1, y + 1, pw, second);
		if ((GetBit(x, y, pw, second) == 1 && n == 2) || n == 3)
		{
			BitSet(x, y, pw, pattern);
		}
	}
	#ifdef GLINTEROP
	Write_image(a, pattern[idy * pw + idx], xoffset, yoffset, idx, idy, reso);
	#else
	Write_image(a, pattern[idy * pw + idx], xoffset, yoffset, idx, idy);
	#endif
	//second[idy * pw + idx] = pattern[idy * pw + idx];
}

__kernel void refresh_arrays(__global write_only uint* pattern, __global uint* second)
{
	uint idz = get_global_id( 0 );
	second[idz] = pattern[idz];
}

__kernel void clear_image(write_only image2d_t a)
{
	uint idx = get_global_id( 0 );
	uint idy = get_global_id( 1 );
	for(int i = 0; i < 32; i++)
	{
		write_imagef(a, (int2)(idx * 32 + i, idy), (float4)(0.0f, 0.0f, 0.0f, 0.0f));
	}
}