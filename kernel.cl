#define GLINTEROP

uint GetBit(uint x, uint y, uint pw, __global uint* second)
{
	return (second[y * pw + (x >> 5)] >> (int)(x & 31)) & 1U; 
}

void BitSet(uint x, uint y, uint pw, __global uint* pattern) 
{
	pattern[y * pw + (x >> 5)] |= 1U << (int)(x & 31); 
}

#ifdef GLINTEROP
__kernel void device_function( write_only image2d_t a, __global write_only uint* pattern, __global uint* second, uint pw, uint ph, uint xoffset, uint yoffset)
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
			if((xoffset <= x) && (x < xoffset + 512) && (yoffset <= y) && (y < yoffset + 512))
			{
			#ifdef GLINTEROP
				int2 pos = (int2)(x - xoffset,y - yoffset);
				write_imagef( a, pos, (float4)(1.0f, 1.0f, 1.0f, 1.0f ) );
			#else
				a[(x - xoffset) + ((y - yoffset) * 512)] = 0xffffff;
			#endif
			}
		}
	}
	//second[idy * pw + idx] = pattern[idy * pw + idx];
}

__kernel void refresh_arrays(__global write_only uint* pattern, __global uint* second)
{
	uint idz = get_global_id( 0 );
	second[idz] = pattern[idz];
}