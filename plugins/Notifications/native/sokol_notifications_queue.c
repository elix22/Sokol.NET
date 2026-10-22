/* sokol_notifications_queue.c -- lock-protected event ring shared by the Android and iOS
   implementations. Platform code emits with sokolnotify__emit() (any thread); the game thread
   drains with sokolnotify_poll_event(). Events carry no strings. */
#include "sokol_notifications.h"

#include <pthread.h>

#define SOKOLNOTIFY_QUEUE_CAP 80   /* a full pending list (63) + DONE + a permission answer */

static pthread_mutex_t _sn_lock = PTHREAD_MUTEX_INITIALIZER;
static sokolnotify_event _sn_ring[SOKOLNOTIFY_QUEUE_CAP];
static int _sn_head = 0;
static int _sn_count = 0;

/* Internal, called by the platform shims. Drops the event if the ring is full
   (the app stopped polling -- nothing better to do). */
void sokolnotify__emit(int type, int value, long long time)
{
    pthread_mutex_lock(&_sn_lock);
    if (_sn_count < SOKOLNOTIFY_QUEUE_CAP) {
        sokolnotify_event* e = &_sn_ring[(_sn_head + _sn_count) % SOKOLNOTIFY_QUEUE_CAP];
        e->type  = type;
        e->value = value;
        e->time  = time;
        _sn_count++;
    }
    pthread_mutex_unlock(&_sn_lock);
}

bool sokolnotify_poll_event(sokolnotify_event* out)
{
    if (!out) return false;
    pthread_mutex_lock(&_sn_lock);
    if (_sn_count == 0) {
        pthread_mutex_unlock(&_sn_lock);
        return false;
    }
    *out = _sn_ring[_sn_head];
    _sn_head = (_sn_head + 1) % SOKOLNOTIFY_QUEUE_CAP;
    _sn_count--;
    pthread_mutex_unlock(&_sn_lock);
    return true;
}
