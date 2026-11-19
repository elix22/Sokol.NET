/* sokol_ads_queue.c -- lock-protected event ring + the ready flag, shared by
   the Android and iOS implementations. Platform code emits with
   sokolads__emit() (any thread); the game thread drains with
   sokolads_poll_event(). Events are two ints — nothing to own or free. */
#include "sokol_ads.h"

#include <pthread.h>
#include <string.h>

#define SOKOLADS_QUEUE_CAP 16

static pthread_mutex_t _sa_lock = PTHREAD_MUTEX_INITIALIZER;
static sokolads_event _sa_ring[SOKOLADS_QUEUE_CAP];
static int _sa_head = 0;   /* next pop */
static int _sa_count = 0;
static bool _sa_ready = false;           /* interstitial */
static bool _sa_rewarded_ready = false;  /* rewarded */

/* Internal, called by the platform shims. LOADED/consumption also maintain the
   ready flag here so sokolads_interstitial_ready() is race-free. */
void sokolads__emit(int type, int code)
{
    pthread_mutex_lock(&_sa_lock);
    if (type == SOKOLADS_EVENT_LOADED)      _sa_ready = true;
    if (type == SOKOLADS_EVENT_SHOWN ||
        type == SOKOLADS_EVENT_LOAD_FAILED) _sa_ready = false;
    if (type == SOKOLADS_EVENT_REWARDED_LOADED) _sa_rewarded_ready = true;
    if (type == SOKOLADS_EVENT_REWARDED_SHOWN ||
        type == SOKOLADS_EVENT_REWARDED_LOAD_FAILED) _sa_rewarded_ready = false;
    if (_sa_count < SOKOLADS_QUEUE_CAP) {
        sokolads_event* e = &_sa_ring[(_sa_head + _sa_count) % SOKOLADS_QUEUE_CAP];
        e->type = type;
        e->code = code;
        _sa_count++;
    }
    pthread_mutex_unlock(&_sa_lock);
}

/* Internal: the show call consumes the loaded ad even before SHOWN arrives. */
void sokolads__consume(void)
{
    pthread_mutex_lock(&_sa_lock);
    _sa_ready = false;
    pthread_mutex_unlock(&_sa_lock);
}

/* Internal: the rewarded show call consumes the loaded rewarded ad. */
void sokolads__consume_rewarded(void)
{
    pthread_mutex_lock(&_sa_lock);
    _sa_rewarded_ready = false;
    pthread_mutex_unlock(&_sa_lock);
}

bool sokolads_rewarded_ready(void)
{
    pthread_mutex_lock(&_sa_lock);
    bool r = _sa_rewarded_ready;
    pthread_mutex_unlock(&_sa_lock);
    return r;
}

bool sokolads_interstitial_ready(void)
{
    pthread_mutex_lock(&_sa_lock);
    bool r = _sa_ready;
    pthread_mutex_unlock(&_sa_lock);
    return r;
}

bool sokolads_poll_event(sokolads_event* out)
{
    if (!out) return false;
    pthread_mutex_lock(&_sa_lock);
    if (_sa_count == 0) {
        pthread_mutex_unlock(&_sa_lock);
        return false;
    }
    *out = _sa_ring[_sa_head];
    _sa_head = (_sa_head + 1) % SOKOLADS_QUEUE_CAP;
    _sa_count--;
    pthread_mutex_unlock(&_sa_lock);
    return true;
}
