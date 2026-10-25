import { pbkdf2Async } from '../lib/noble-hashes/pbkdf2.js';
import { sha3_256 } from '../lib/noble-hashes/sha3.js';

// RPCS3 rpcn_settings_dialog.cpp::derive_password, rpcn.yml Version 2.
// https://github.com/RPCS3/rpcs3/blob/master/rpcs3/rpcs3qt/rpcn_settings_dialog.cpp
export async function createRpcnYaml({ username, password, token = '' }) {
    if (typeof username !== 'string' || !/^[A-Za-z0-9_-]{3,16}$/.test(username)
        || typeof password !== 'string' || !password.length || password.length > 1024
        || typeof token !== 'string' || (token !== '' && !/^[A-Za-z0-9]{16}$/.test(token))) {
        throw new Error('Invalid RPCN account fields');
    }
    const encoder = new TextEncoder();
    const bytes = encoder.encode(password);
    let derived;
    try {
        derived = await pbkdf2Async(sha3_256, bytes,
            encoder.encode("No matter where you go, everybody's connected."),
            { c: 200000, dkLen: 32, asyncTick: 16 });
        const hash = Array.from(derived, b => b.toString(16).padStart(2, '0')).join('').toUpperCase();
        return '# Keep this file private. Copy to /dev_hdd0/tmp/rpcn.yml on PS3.\n'
            + 'Version: 2\nHost: np.rpcs3.net\n'
            + `NPID: "${username}"\nPassword: "${hash}"\nToken: "${token}"\n`
            + 'Hosts: Official RPCN Server|np.rpcs3.net\nExperimental IPv6 support: false\n';
    } finally {
        bytes.fill(0);
        if (derived) derived.fill(0);
    }
}
