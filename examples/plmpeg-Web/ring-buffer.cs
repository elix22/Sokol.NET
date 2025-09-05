using Sokol;
using System.Runtime.InteropServices;
using static Sokol.SG;
public static unsafe partial class PlMpegApp
{
    //=== a simple ring buffer implementation ====================================*/
    static uint ring_wrap(uint i)
    {
        return i % RING_NUM_SLOTS;
    }

    static bool ring_full(ring_t rb)
    {
        return ring_wrap(rb.head + 1) == rb.tail;
    }

    static bool ring_empty(ring_t rb)
    {
        return rb.head == rb.tail;
    }

    static uint ring_count(ring_t rb)
    {
        uint count;
        if (rb.head >= rb.tail)
        {
            count = rb.head - rb.tail;
        }
        else
        {
            count = (rb.head + RING_NUM_SLOTS) - rb.tail;
        }
        return count;
    }

    static void ring_enqueue(ring_t rb, int val)
    {
        // assert(!ring_full(rb));
        rb.buf[rb.head] = val;
        rb.head = ring_wrap(rb.head + 1);
    }

    static int ring_dequeue(ring_t rb)
    {
        if (ring_empty(rb))
        {
            return -1;
        }
        // assert(!ring_empty(rb));
        int slot_id = rb.buf[rb.tail];
        rb.tail = ring_wrap(rb.tail + 1);
        return slot_id;
    }

}