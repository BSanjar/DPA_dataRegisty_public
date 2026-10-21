/*
 * Реквизиты организации, указываемые вручную (Views/Shared/_OrganizationDetailsFields.cshtml):
 * маски ввода и проверки полей. Те же правила на сервере — Models/local_models/OrganizationDetailsInput.cs.
 *
 *   OrgDetails.check(el)   → текст ошибки или null (для своего валидатора страницы, как в мастере инцидента);
 *   <form data-org-form>   → проверка при уходе с поля и при отправке (страница профиля).
 */
(function () {
    'use strict';

    const LETTERS = 'A-Za-zА-Яа-яЁёӨөҮүҢң';
    const PERSON_RE = new RegExp('^[' + LETTERS + '][' + LETTERS + "'’\\- ]*$");
    const PERSON_STRIP_RE = new RegExp('[^' + LETTERS + "'’\\- ]+", 'g');
    const REGNUM_RE = new RegExp('^(?=.*\\d)[0-9' + LETTERS + '\\-/.]+$');
    const REGNUM_STRIP_RE = new RegExp('[^0-9' + LETTERS + '\\-/.]+', 'g');
    const MIN_DATE = '1991-01-01';
    // Части кыргызских и тюркских отчеств пишутся со строчной: «Асанов Бакыт уулу».
    const LOWER_WORDS = ['уулу', 'кызы', 'оглы', 'гызы'];

    function squash(v) { return v.replace(/\s+/g, ' ').trim(); }
    function todayIso() {
        const d = new Date();
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    function capitalizePerson(v) {
        return squash(v).split(' ').map(word => {
            if (LOWER_WORDS.includes(word.toLowerCase())) return word.toLowerCase();
            // «Абдыкадыров-Салиев» — заглавная после дефиса тоже
            return word.split('-').map(p => p ? p.charAt(0).toUpperCase() + p.slice(1) : p).join('-');
        }).join(' ');
    }
    // "Пример" → «Пример» (парные прямые кавычки в наименовании)
    function fixQuotes(v) {
        let open = true;
        return v.replace(/["“”„]/g, () => { const q = open ? '«' : '»'; open = !open; return q; });
    }

    /** Ошибка поля или null. Правила: data-required, data-min-length, maxlength, data-validate="org-*". */
    function check(el) {
        const v = (el.value || '').trim();
        if (el.dataset.required === 'true' && !v) return 'Поле обязательно для заполнения';
        if (!v) return null;

        const min = Number(el.dataset.minLength || 0);
        if (min && v.length < min) return 'Минимум ' + min + ' символов';
        const max = Number(el.getAttribute('maxlength') || 0);
        if (max && v.length > max) return 'Не длиннее ' + max + ' символов';

        switch (el.dataset.validate) {
            case 'org-person':
                if (!PERSON_RE.test(v)) return 'Только буквы, пробел, дефис и апостроф';
                if (squash(v).split(' ').length < 2) return 'Укажите фамилию и имя';
                break;
            case 'org-regnum':
                if (v.length < 3) return 'Минимум 3 символа';
                if (!REGNUM_RE.test(v)) return 'Буквы, цифры, «-», «/», «.»; хотя бы одна цифра';
                break;
            case 'org-date':
                if (!/^\d{4}-\d{2}-\d{2}$/.test(v) || isNaN(new Date(v + 'T00:00:00'))) return 'Некорректная дата';
                if (v > todayIso()) return 'Дата не может быть в будущем';
                if (v < MIN_DATE) return 'Дата не раньше 01.01.1991';
                break;
        }
        return null;
    }

    // ===== маски =====
    function setValue(input, value) {
        if (value === input.value) return;
        const pos = input.selectionStart, diff = input.value.length - value.length;
        input.value = value;
        if (pos != null && document.activeElement === input) {
            const p = Math.max(0, pos - diff);
            input.setSelectionRange(p, p);
        }
    }

    const MASKS = {
        'person-name': {
            input: v => v.replace(PERSON_STRIP_RE, '').replace(/\s{2,}/g, ' ').replace(/^\s+/, ''),
            blur: capitalizePerson
        },
        'regnum': {
            input: v => v.replace(/\s+/g, '').replace(REGNUM_STRIP_RE, '').toUpperCase(),
            blur: v => v.replace(/^[-/.]+|[-/.]+$/g, '')
        },
        'org-name': {
            input: v => v.replace(/^\s+/, '').replace(/\s{2,}/g, ' '),
            blur: v => fixQuotes(squash(v))
        },
        'squash': {
            input: v => v.replace(/^\s+/, '').replace(/\s{2,}/g, ' '),
            blur: squash
        }
    };

    function applyMasks(root) {
        root.querySelectorAll('[data-mask]').forEach(input => {
            const mask = MASKS[input.dataset.mask];
            if (!mask || input.dataset.maskBound) return;
            input.dataset.maskBound = '1';
            input.addEventListener('input', () => setValue(input, mask.input(input.value)));
            // blur-обработчик ставится раньше проверки страницы — проверяется уже приведённое значение
            input.addEventListener('blur', () => { input.value = mask.blur(input.value); }, true);
        });
    }

    // ===== самостоятельная форма (профиль) =====
    function showError(el, message) {
        const err = el.parentElement.querySelector('.field-error');
        el.classList.toggle('is-invalid', !!message);
        if (err) { err.textContent = message || ''; err.classList.toggle('visible', !!message); }
    }

    function attachForm(form) {
        const fields = Array.from(form.querySelectorAll('[data-org-fields] input:not([readonly]):not([type="hidden"]), [data-org-fields] select'));
        const validate = el => { const m = check(el); showError(el, m); return !m; };
        fields.forEach(el => {
            el.addEventListener('blur', () => validate(el));
            el.addEventListener('change', () => validate(el));
            el.addEventListener('input', () => { if (el.classList.contains('is-invalid')) validate(el); });
        });
        form.addEventListener('submit', e => {
            let first = null;
            fields.forEach(el => { if (!validate(el) && !first) first = el; });
            if (first) {
                e.preventDefault();
                first.focus({ preventScroll: true });
                first.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        });
        // ошибки, пришедшие с сервера
        form.querySelectorAll('.field-error').forEach(span => {
            if (span.textContent.trim()) {
                span.classList.add('visible');
                span.parentElement.querySelector('input, select, textarea')?.classList.add('is-invalid');
            }
        });
    }

    document.querySelectorAll('[data-org-fields]').forEach(applyMasks);
    document.querySelectorAll('form[data-org-form]').forEach(attachForm);

    window.OrgDetails = { check: check };
})();
