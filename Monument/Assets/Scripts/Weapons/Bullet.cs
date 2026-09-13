using UnityEngine;

public class Bullet : MonoBehaviour
{

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.gameObject.CompareTag("Entity"))
        {
            print("hit " + collision.gameObject.name + "!");

			CreateBulletImpactEffect(collision);

            Destroy(gameObject);
        }
		else
		{
			CreateBulletImpactEffect(collision);

			Destroy(gameObject);
		}
	}

	void CreateBulletImpactEffect(Collision objectHit)
	{
		ContactPoint contact = objectHit.contacts[0];

		GameObject hole = Instantiate(
			GlobalReferences.Instance.bulletImpactEffectPrefab,
			contact.point,
			Quaternion.LookRotation(contact.normal)
			);

		hole.transform.SetParent(objectHit.gameObject.transform);
	}
}
