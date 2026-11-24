// Home page motion (GSAP + ScrollTrigger). Content is fully visible without JavaScript,
// without GSAP (CDN blocked) and for visitors who prefer reduced motion.
// "Just arrived": hovering (or focusing) a row brings that product's photo forward.
// Works with plain CSS fades; GSAP, when present, adds a wipe-and-zoom reveal.
(() => {
    'use strict';

    const section = document.querySelector('[data-swap]');
    if (!section) return;
    const imgs = [...section.querySelectorAll('[data-swap-img]')];
    const rows = [...section.querySelectorAll('[data-swap-row]')];
    const caption = section.querySelector('[data-swap-caption]');
    const motion = () => window.gsap && !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let current = imgs.find((i) => i.classList.contains('is-active'));

    const show = (row) => {
        rows.forEach((r) => r.classList.toggle('is-active', r === row));
        const next = imgs.find((i) => i.dataset.swapImg === row.dataset.swapRow);
        if (!next || next === current) return;
        if (caption) caption.textContent = row.dataset.swapName || '';
        const prev = current;
        current = next;
        imgs.forEach((i) => { i.style.zIndex = i === next ? 2 : (i === prev ? 1 : 0); });
        if (motion()) {
            const { gsap } = window;
            gsap.killTweensOf(next);
            gsap.set(next, { opacity: 1 });
            gsap.fromTo(next, { clipPath: 'inset(0 0 100% 0)', scale: 1.12 }, {
                clipPath: 'inset(0 0 0% 0)', scale: 1, duration: 0.75, ease: 'power3.out',
                onComplete: () => { if (prev && prev !== current) prev.classList.remove('is-active'); },
            });
            next.classList.add('is-active');
        } else {
            next.classList.add('is-active');
            if (prev) prev.classList.remove('is-active');
        }
    };

    rows.forEach((row) => {
        row.addEventListener('mouseenter', () => show(row));
        row.addEventListener('focusin', () => show(row));
    });
})();

(() => {
    'use strict';

    const root = document.documentElement;
    const home = document.querySelector('[data-home]');
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!home || !window.gsap || reduce) {
        root.classList.remove('home-anim');
        return;
    }

    const { gsap } = window;
    if (window.ScrollTrigger) gsap.registerPlugin(window.ScrollTrigger);
    const hasST = Boolean(window.ScrollTrigger);
    const q = (sel, scope = home) => [...scope.querySelectorAll(sel)];

    // ---------- hero intro ----------
    gsap.set(q('[data-hero-line], [data-hero-in], [data-hero-main], [data-hero-cut], [data-orb]'), { visibility: 'visible' });
    root.classList.remove('home-anim');

    const intro = gsap.timeline({ defaults: { ease: 'power4.out' } });
    intro
        .from(q('[data-hero-line]'), { yPercent: 110, duration: 1.15, stagger: 0.12 })
        .from(q('[data-hero-main] img'), { clipPath: 'polygon(0 100%, 100% 100%, 100% 100%, 0 100%)', scale: 1.18, duration: 1.4, ease: 'expo.out' }, 0.15)
        .from(q('[data-hero-cut]'), { x: 80, autoAlpha: 0, duration: 1.1 }, 0.55)
        .from(q('[data-orb]'), { scale: 0, rotate: -120, duration: 0.9, ease: 'back.out(2.2)' }, 0.8)
        .from(q('[data-hero-in]'), { y: 30, autoAlpha: 0, duration: 0.9, stagger: 0.1 }, 0.6);

    // Count the "pieces ready" number up.
    q('[data-count]').forEach((el) => {
        const target = parseInt(el.dataset.count, 10) || 0;
        const counter = { v: 0 };
        intro.to(counter, { v: target, duration: 1.6, ease: 'power2.out', onUpdate: () => { el.textContent = Math.round(counter.v); } }, 0.7);
    });

    // The accent orb keeps floating gently.
    gsap.to(q('[data-orb]'), { y: -14, rotate: 18, duration: 2.6, ease: 'sine.inOut', yoyo: true, repeat: -1, delay: 1.7 });

    if (!hasST) return;
    const { ScrollTrigger } = window;

    // ---------- hero: scroll-driven scene ----------
    // Desktop: the hero stays pinned while scrolling plays a short scene. Smaller screens get a light parallax.
    const mm = gsap.matchMedia();
    const lines = q('.drq-line');
    mm.add('(min-width: 801px)', () => {
        const scene = gsap.timeline({
            defaults: { ease: 'none' },
            scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: '+=90%', scrub: 0.6, pin: true, anticipatePin: 1 },
        });
        scene
            .to(lines[0] || [], { xPercent: -14 }, 0)
            .to(lines[1] || [], { xPercent: 12 }, 0)
            .to('.drq-title', { autoAlpha: 0.15, y: -40 }, 0.35)
            .to('[data-hero-main]', { scale: 0.86, yPercent: -6, borderRadius: 18 }, 0)
            .to('[data-hero-main] img', { scale: 1.12 }, 0)
            .to('[data-hero-cut]', { yPercent: -45, rotate: -3 }, 0)
            .to('[data-orb]', { xPercent: -260, yPercent: 220, rotate: 300 }, 0)
            .to('.drq-hero-left', { yPercent: -30, autoAlpha: 0 }, 0.2)
            .to('.drq-hero-note, .drq-hero-social', { autoAlpha: 0 }, 0.1);
        return () => gsap.set(['.drq-title', '[data-hero-main]', '.drq-hero-left', '.drq-hero-note', '.drq-hero-social'], { clearProps: 'all' });
    });
    mm.add('(max-width: 800px)', () => {
        gsap.to(q('[data-hero-main] img'), { yPercent: 8, ease: 'none', scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: 'bottom top', scrub: true } });
        gsap.to(lines[0] || [], { xPercent: -8, ease: 'none', scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: 'bottom top', scrub: true } });
        gsap.to(lines[1] || [], { xPercent: 8, ease: 'none', scrollTrigger: { trigger: '.drq-hero', start: 'top top', end: 'bottom top', scrub: true } });
    });

    // ---------- generic reveals ----------
    ScrollTrigger.batch(q('[data-reveal]'), {
        start: 'top 86%',
        once: true,
        onEnter: (els) => gsap.from(els, { y: 50, autoAlpha: 0, duration: 1, ease: 'power3.out', stagger: 0.12, overwrite: true }),
    });

    // Photos open like a curtain, then drift slightly while scrolling.
    q('[data-reveal-photo]').forEach((fig) => {
        const img = fig.querySelectorAll('img');
        gsap.from(img, { scale: 1.25, duration: 1.6, ease: 'expo.out', scrollTrigger: { trigger: fig, start: 'top 85%', once: true } });
        gsap.from(fig, { clipPath: 'polygon(0 0, 0 0, 0 100%, 0 100%)', duration: 1.3, ease: 'power4.inOut', scrollTrigger: { trigger: fig, start: 'top 85%', once: true },
            onComplete: () => gsap.set(fig, { clearProps: 'clipPath' }) });
        gsap.to(img, { yPercent: -6, ease: 'none', scrollTrigger: { trigger: fig, start: 'top bottom', end: 'bottom top', scrub: true } });
    });

    // List rows (categories, new arrivals) slide in one after another.
    ScrollTrigger.batch(q('[data-list-item]'), {
        start: 'top 92%',
        once: true,
        onEnter: (els) => gsap.from(els, { x: -40, autoAlpha: 0, duration: 0.8, ease: 'power3.out', stagger: 0.07 }),
    });

    // ---------- dark mood frames ----------
    const frames = q('[data-mood-frame]');
    if (frames.length) {
        gsap.from(frames, {
            y: 120, autoAlpha: 0, rotate: (i) => [-4, 0, 4][i % 3], duration: 1.3, ease: 'power4.out', stagger: 0.15,
            scrollTrigger: { trigger: '.drq-mood-frames', start: 'top 80%', once: true },
        });
        frames.forEach((frame, i) => {
            gsap.to(frame, { y: [-40, -90, -20][i % 3], ease: 'none', scrollTrigger: { trigger: '.drq-mood', start: 'top bottom', end: 'bottom top', scrub: true } });
        });
    }

    // ---------- featured cards ----------
    ScrollTrigger.batch(q('[data-card]'), {
        start: 'top 90%',
        once: true,
        onEnter: (els) => gsap.from(els, { y: 60, autoAlpha: 0, duration: 0.9, ease: 'power3.out', stagger: 0.08 }),
    });

    // Re-animate the cards that stay visible when a filter is chosen (filtering itself lives in storefront.js).
    const filters = home.querySelector('[data-collection-filters]');
    if (filters) {
        filters.addEventListener('click', (e) => {
            if (!e.target.closest('button')) return;
            requestAnimationFrame(() => {
                const shown = q('[data-card]').filter((c) => !c.hidden);
                gsap.fromTo(shown, { y: 24, autoAlpha: 0, scale: 0.97 }, { y: 0, autoAlpha: 1, scale: 1, duration: 0.55, ease: 'power3.out', stagger: 0.05, overwrite: true });
                ScrollTrigger.refresh();
            });
        });
    }

    // Images that load late change the layout: keep trigger positions accurate.
    window.addEventListener('load', () => ScrollTrigger.refresh());
})();
