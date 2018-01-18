uint GetBit(uint x, uint y, uint pw, __global uint* second)
{
	return (second[y * pw + (x >> 5)] >> (int)(x & 31)) & 1U; 
}

void BitSet(uint x, uint y, uint pw, __global uint* pattern) 
{
	pattern[y * pw + (x >> 5)] |= 1U << (int)(x & 31); 
}

__kernel void device_function( __global int* a, __global uint* pattern, __global uint* second, uint pw, uint ph, uint xoffset, uint yoffset)
{
	uint idx = get_global_id( 0 );
	uint idy = get_global_id( 1 );
	a[idx + idy * pw] = idx + (idy * pw);
	pattern[idy * pw + idx] = 0;
	if(idy == 0 || idy == ph - 1) return;

	for(int i = 0; i < 64; i++)
	{
		uint x = (idx * pw) + i;
		uint y = idy;
		if(x == 0 || x == pw * 32 - 1) continue;
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
			//if((xoffset <= x) && (x < xoffset + 512) && (yoffset <= y) && (y < yoffset + 512)) a[(x - xoffset) + ((y - yoffset) * 512)] = 0xffffff;
		}
	}
	//second[idy * pw + idx] = pattern[idy * pw + idx];
}